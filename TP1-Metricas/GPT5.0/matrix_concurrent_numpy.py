#!/usr/bin/env python3
"""
matrix_concurrent_numpy.py
Implementación alternativa usando NumPy para multiplicación de matrices enteras:
- Modos:
  * direct         -> A @ B
  * blocked        -> triple bucle tile (i,j,k)
  * hybrid-process -> divide A en chunks y usa procesos con np.dot (requiere BLAS interno limitado a 1 hilo para ser útil)
- Patrones de generación iguales a la versión pura.
- Export JSON de métricas.
"""

from __future__ import annotations
import time
import json
import argparse
import math
import os
from typing import Callable, Optional, Dict, Any, Sequence, Tuple
import numpy as np
from concurrent.futures import ProcessPoolExecutor, as_completed


# ---------------------------------------
# Patrones de generación
# ---------------------------------------

def make_pattern_function(pattern: str, rows: int, cols: int, const_value: int):
    pattern = pattern.lower()
    if pattern == "random":
        # Se generará de un tirón con randint
        return None
    if pattern == "sequential":
        return lambda: np.arange(rows*cols, dtype=np.int64).reshape(rows, cols)
    if pattern == "ij":
        # i + j
        I = np.arange(rows, dtype=np.int64).reshape(rows, 1)
        J = np.arange(cols, dtype=np.int64).reshape(1, cols)
        return lambda: (I + J)
    if pattern == "checker":
        I = np.arange(rows, dtype=np.int64).reshape(rows, 1)
        J = np.arange(cols, dtype=np.int64).reshape(1, cols)
        return lambda: ( (I + J) & 1 )
    if pattern == "constant":
        return lambda: np.full((rows, cols), const_value, dtype=np.int64)
    if pattern == "identitya" or pattern == "identityb":
        # Identity (si rectangular se rellena diagonal hasta min(rows, cols))
        def build():
            M = np.zeros((rows, cols), dtype=np.int64)
            d = min(rows, cols)
            M[np.arange(d), np.arange(d)] = 1
            return M
        return build
    raise ValueError(f"Patrón desconocido: {pattern}")


def generate_matrix(rows: int, cols: int, pattern: str, seed: Optional[int], const_value: int) -> np.ndarray:
    if seed is not None:
        np.random.seed(seed)
    fn = make_pattern_function(pattern, rows, cols, const_value)
    if pattern == "random":
        # Rango moderado para evitar overflow en tests comparativos
        return np.random.randint(-10, 11, size=(rows, cols), dtype=np.int64)
    return fn().astype(np.int64, copy=False)


# ---------------------------------------
# Multiplicación directa
# ---------------------------------------

def multiply_direct(A: np.ndarray, B: np.ndarray) -> np.ndarray:
    return A @ B  # np.matmul


# ---------------------------------------
# Multiplicación bloqueada (i,j,k)
# ---------------------------------------

def multiply_blocked(A: np.ndarray, B: np.ndarray, block: int) -> np.ndarray:
    """
    Bloqueo sencillo (m,n,p grandes) iterando en tiles cúbicos aproximados.
    Para simplicidad, un solo parámetro block se usa en i, j y k.
    """
    m, n = A.shape
    n2, p = B.shape
    if n != n2:
        raise ValueError("Dimensiones incompatibles")
    C = np.zeros((m, p), dtype=np.int64)
    for i0 in range(0, m, block):
        i1 = min(i0 + block, m)
        for k0 in range(0, n, block):
            k1 = min(k0 + block, n)
            # Sub-bloque A[i0:i1, k0:k1]
            A_block = A[i0:i1, k0:k1]
            for j0 in range(0, p, block):
                j1 = min(j0 + block, p)
                B_block = B[k0:k1, j0:j1]
                # Acumula en C sub-bloque
                # (A_block) shape (ib, kb) dot (B_block) (kb, jb) -> (ib, jb)
                C[i0:i1, j0:j1] += A_block @ B_block
    return C


# ---------------------------------------
# Híbrido por procesos
# ---------------------------------------

def _chunk_dot(A_chunk: np.ndarray, B: np.ndarray, start_row: int) -> Tuple[int, np.ndarray]:
    # Retorna (fila_inicial, resultado_parcial)
    return start_row, A_chunk @ B

def multiply_hybrid_process(A: np.ndarray, B: np.ndarray, workers: int, chunks: int) -> np.ndarray:
    m, n = A.shape
    if chunks <= 0:
        chunks = workers
    chunk_size = math.ceil(m / chunks)
    futures = []
    C = np.zeros((m, B.shape[1]), dtype=np.int64)
    with ProcessPoolExecutor(max_workers=workers) as ex:
        for start in range(0, m, chunk_size):
            end = min(start + chunk_size, m)
            # Copia del slice (inevitable sin shared memory)
            A_slice = A[start:end].copy()
            futures.append(ex.submit(_chunk_dot, A_slice, B, start))
        for fut in as_completed(futures):
            start_row, partial = fut.result()
            C[start_row:start_row+partial.shape[0], :] = partial
    return C


# ---------------------------------------
# Verificación (comparación)
# ---------------------------------------

def verify_equal(C1: np.ndarray, C2: np.ndarray) -> bool:
    if C1.shape != C2.shape:
        return False
    return np.array_equal(C1, C2)


# ---------------------------------------
# Lógica principal
# ---------------------------------------

def run_multiplication(rowsA: int, colsA: int, colsB: int,
                       patternA: str, patternB: str,
                       constA: int, constB: int,
                       seedA: Optional[int], seedB: Optional[int],
                       mode: str, block: int,
                       workers: int, chunks: int,
                       verify: bool, json_out: str) -> None:

    # Generar matrices
    t_gen_s = time.perf_counter()
    A = generate_matrix(rowsA, colsA, patternA, seedA, constA)
    B = generate_matrix(colsA, colsB, patternB, seedB, constB)
    t_gen_e = time.perf_counter()

    # Multiplicación
    t_mul_s = time.perf_counter()
    if mode == "direct":
        C = multiply_direct(A, B)
    elif mode == "blocked":
        if block <= 0:
            raise ValueError("Block size debe ser > 0")
        C = multiply_blocked(A, B, block=block)
    elif mode == "hybrid-process":
        if workers <= 0:
            import os
            workers = os.cpu_count() or 1
        C = multiply_hybrid_process(A, B, workers=workers, chunks=chunks)
    else:
        raise ValueError("Modo inválido")
    t_mul_e = time.perf_counter()

    # Verificación opcional comparando contra direct
    verification_meta = {}
    if verify and mode != "direct":
        t_ver_s = time.perf_counter()
        C_ref = multiply_direct(A, B)
        ok = verify_equal(C, C_ref)
        t_ver_e = time.perf_counter()
        verification_meta = {
            "verified_equal_to_direct": ok,
            "verify_time": t_ver_e - t_ver_s
        }
        if not ok:
            verification_meta["note"] = "Resultado difiere de la referencia (direct)."

    # Métricas
    mult_theoretical = rowsA * colsA * colsB
    add_theoretical = rowsA * (colsA - 1) * colsB if colsA > 0 else 0

    meta: Dict[str, Any] = {
        "mode": mode,
        "dimensions": {"A":[rowsA, colsA], "B":[colsA, colsB], "C":[rowsA, colsB]},
        "patterns": {"A": patternA, "B": patternB},
        "generation_time": t_gen_e - t_gen_s,
        "multiplication_time": t_mul_e - t_mul_s,
        "wall_total": (t_mul_e - t_mul_s) + (t_gen_e - t_gen_s),
        "operations_theoretical": {
            "multiplications": mult_theoretical,
            "additions": add_theoretical,
            "total": mult_theoretical + add_theoretical
        }
    }

    if mode == "blocked":
        meta["block"] = block
    if mode == "hybrid-process":
        meta["workers"] = workers
        meta["chunks"] = chunks

    meta.update(verification_meta)

    # Resumen
    print("=== RESUMEN NUMPY ===")
    print(f"Dimensiones: A={rowsA}x{colsA}, B={colsA}x{colsB}")
    print(f"Modo: {mode}")
    if mode == "blocked":
        print(f"Block size: {block}")
    if mode == "hybrid-process":
        print(f"Workers externos: {workers}  Chunks: {chunks}")
    print(f"Tiempo generación: {meta['generation_time']:.6f}s")
    print(f"Tiempo multiplicación: {meta['multiplication_time']:.6f}s")
    print(f"Tiempo total: {meta['wall_total']:.6f}s")
    if verify and mode != "direct":
        print(f"Verificación contra 'direct': {meta.get('verified_equal_to_direct')} (tiempo {meta.get('verify_time',0):.6f}s)")

    if json_out:
        with open(json_out, "w", encoding="utf-8") as f:
            json.dump(meta, f, indent=2)
        print(f"[INFO] Exportado JSON a {json_out}")

    # Mostrar C si pequeña
    if rowsA <= 5 and colsB <= 5:
        print("Matriz resultado C:")
        print(C)


# ---------------------------------------
# CLI
# ---------------------------------------

def parse_args():
    ap = argparse.ArgumentParser(description="Multiplicación de matrices con NumPy (modos: direct, blocked, hybrid-process).")
    ap.add_argument("--rowsA", type=int, default=400)
    ap.add_argument("--colsA", type=int, default=500)
    ap.add_argument("--colsB", type=int, default=300)
    ap.add_argument("--patternA", default="random")
    ap.add_argument("--patternB", default="random")
    ap.add_argument("--const-value", type=int, default=1, help="Valor para patrón constant (se usa en ambas si corresponde)")
    ap.add_argument("--seedA", type=int, default=123)
    ap.add_argument("--seedB", type=int, default=456)
    ap.add_argument("--mode", choices=["direct","blocked","hybrid-process"], default="direct")
    ap.add_argument("--block", type=int, default=128, help="Tamaño de bloque (modo blocked)")
    ap.add_argument("--workers", type=int, default=0, help="Workers externos (modo hybrid-process)")
    ap.add_argument("--chunks", type=int, default=0, help="N° de chunks (modo hybrid-process), 0=igual a workers")
    ap.add_argument("--verify", action="store_true", help="Verificar contra modo direct (si mode != direct)")
    ap.add_argument("--json-out", default="", help="Archivo para exportar JSON")
    return ap.parse_args()


def main():
    args = parse_args()
    run_multiplication(rowsA=args.rowsA,
                       colsA=args.colsA,
                       colsB=args.colsB,
                       patternA=args.patternA,
                       patternB=args.patternB,
                       constA=args.const_value,
                       constB=args.const_value,
                       seedA=args.seedA,
                       seedB=args.seedB,
                       mode=args.mode,
                       block=args.block,
                       workers=args.workers,
                       chunks=args.chunks,
                       verify=args.verify,
                       json_out=args.json_out)


if __name__ == "__main__":
    main()