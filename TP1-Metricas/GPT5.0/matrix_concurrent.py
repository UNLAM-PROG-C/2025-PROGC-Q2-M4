#!/usr/bin/env python3
"""
matrix_concurrent.py
Multiplicación concurrente de matrices de enteros:
 - Modo naive (por filas)
 - Modo blocked (tiles completos)
 - Concurrency: threads o processes
 - Conteo de operaciones
 - Benchmark / escalado
 - Exportación JSON
 - Patrones deterministas
"""

from __future__ import annotations
import time
import math
import random
import argparse
import json
from concurrent.futures import ThreadPoolExecutor, ProcessPoolExecutor, as_completed
from dataclasses import dataclass
from typing import List, Tuple, Sequence, Optional, Dict, Any, Callable

# -----------------------------
# Tipos
# -----------------------------
IntMatrix = List[List[int]]

@dataclass
class WorkResultRows:
    start_row: int
    end_row: int
    rows: IntMatrix
    multiplications: int
    additions: int

@dataclass
class WorkResultTile:
    i0: int
    j0: int
    tile: IntMatrix
    multiplications: int
    additions: int


# -----------------------------
# Generación de matrices
# -----------------------------

def make_pattern_function(pattern: str, rows: int, cols: int, const_value: int) -> Callable[[int,int], int]:
    pattern = pattern.lower()
    if pattern == "random":
        # Se usará random.randint afuera
        return lambda i, j: random.randint(-10, 10)
    if pattern == "sequential":
        return lambda i, j: i * cols + j
    if pattern == "ij":
        return lambda i, j: i + j
    if pattern == "checker":
        return lambda i, j: (i + j) & 1
    if pattern == "constant":
        return lambda i, j: const_value
    if pattern == "identitya":
        # Para A: 1 en diagonal si cuadrada
        if rows != cols:
            return lambda i, j: 1 if i == j else 0  # still works, but rectangular "identity" partial
        return lambda i, j: 1 if i == j else 0
    if pattern == "identityb":
        # Similar idea para B
        if rows != cols:
            return lambda i, j: 1 if i == j else 0
        return lambda i, j: 1 if i == j else 0
    raise ValueError(f"Patrón desconocido: {pattern}")


def generate_matrix(rows: int, cols: int, pattern: str = "random",
                    seed: Optional[int] = None, const_value: int = 0) -> IntMatrix:
    if rows <= 0 or cols <= 0:
        raise ValueError("Dimensiones inválidas")
    if seed is not None:
        random.seed(seed)
    fn = make_pattern_function(pattern, rows, cols, const_value)
    # Para random usamos la función random.randint en cada celda; para otros patrones es determinista
    return [[fn(i, j) for j in range(cols)] for i in range(rows)]


def transpose(M: IntMatrix) -> IntMatrix:
    if not M:
        return []
    return [list(col) for col in zip(*M)]


# -----------------------------
# Secuencial (para verificación)
# -----------------------------

def multiply_sequential(A: IntMatrix, B: IntMatrix) -> IntMatrix:
    m = len(A)
    n = len(A[0])
    if n != len(B):
        raise ValueError("Dimensiones incompatibles")
    p = len(B[0])
    BT = transpose(B)
    C: IntMatrix = [[0]*p for _ in range(m)]
    for i in range(m):
        rowA = A[i]
        for j, colB in enumerate(BT):
            acc = 0
            first = True
            for a,b in zip(rowA, colB):
                prod = a * b
                if first:
                    acc = prod
                    first = False
                else:
                    acc += prod
            C[i][j] = acc
    return C


# -----------------------------
# NAIVE: Trabajo por filas
# -----------------------------

def _compute_rows(A: IntMatrix, BT: IntMatrix, start: int, end: int) -> WorkResultRows:
    p = len(BT)
    multiplications = 0
    additions = 0
    out_rows: IntMatrix = []
    for i in range(start, end):
        rowA = A[i]
        new_row = []
        for col in BT:
            first = True
            acc = 0
            for a, b in zip(rowA, col):
                prod = a * b
                multiplications += 1
                if first:
                    acc = prod
                    first = False
                else:
                    acc += prod
                    additions += 1
            new_row.append(acc)
        out_rows.append(new_row)
    return WorkResultRows(start, end, out_rows, multiplications, additions)


def multiply_matrices_naive(A: IntMatrix,
                            B: IntMatrix,
                            workers: int,
                            method: str,
                            chunk_size: Optional[int],
                            collect_stats: bool,
                            timing_detail: bool) -> Tuple[IntMatrix, Dict[str, Any]]:
    t0 = time.perf_counter()
    cpu0 = time.process_time()

    m = len(A)
    n = len(A[0])
    p = len(B[0])

    t_prep_s = time.perf_counter()
    BT = transpose(B)
    t_prep_e = time.perf_counter()

    if chunk_size is None:
        chunk_size = max(1, m // (workers * 4) or 1)
        if chunk_size > 5000:
            chunk_size = 5000

    ranges = []
    s = 0
    while s < m:
        e = min(s + chunk_size, m)
        ranges.append((s,e))
        s = e

    exec_cls = ProcessPoolExecutor if method == "process" else ThreadPoolExecutor
    futures = []
    partial: Dict[int, WorkResultRows] = {}
    total_mult = 0
    total_add = 0

    t_comp_s = time.perf_counter()
    with exec_cls(max_workers=workers) as executor:
        for (rs,re) in ranges:
            futures.append(executor.submit(_compute_rows, A, BT, rs, re))
        for fut in as_completed(futures):
            wr = fut.result()
            partial[wr.start_row] = wr
            total_mult += wr.multiplications
            total_add += wr.additions
    t_comp_e = time.perf_counter()

    t_asm_s = time.perf_counter()
    C: IntMatrix = [[0]*p for _ in range(m)]
    for start_row in sorted(partial.keys()):
        wr = partial[start_row]
        offset = wr.start_row
        for local_idx, row in enumerate(wr.rows):
            C[offset + local_idx] = row
    t_asm_e = time.perf_counter()

    t1 = time.perf_counter()
    cpu1 = time.process_time()

    meta: Dict[str, Any] = {}
    if collect_stats:
        theoretical_mult = m * n * p
        theoretical_add = m * (n-1) * p if n>0 else 0
        meta = {
            "mode": "naive",
            "method": method,
            "dimensions": {"A":[m,n],"B":[n,p],"C":[m,p]},
            "workers": workers,
            "chunk_size": chunk_size,
            "chunks": len(ranges),
            "operations": {
                "multiplications": total_mult,
                "additions": total_add,
                "total": total_mult + total_add,
                "theoretical": {
                    "multiplications": theoretical_mult,
                    "additions": theoretical_add,
                    "total": theoretical_mult + theoretical_add
                }
            },
            "timing": {
                "wall_total": t1 - t0,
                "cpu_time_main": cpu1 - cpu0
            }
        }
        if timing_detail:
            meta["timing"]["phases"] = {
                "prepare": t_prep_e - t_prep_s,
                "dispatch_compute": t_comp_e - t_comp_s,
                "assemble": t_asm_e - t_asm_s
            }
    return C, meta


# -----------------------------
# BLOCKED: Trabajo por tiles
# -----------------------------

def _compute_tile(A: IntMatrix, B: IntMatrix,
                  i0: int, i1: int,
                  j0: int, j1: int) -> WorkResultTile:
    # A shape m x n; B shape n x p
    n = len(A[0])
    rows_tile = i1 - i0
    cols_tile = j1 - j0
    tile: IntMatrix = [[0]*cols_tile for _ in range(rows_tile)]
    mults = 0
    adds = 0
    # Para cada celda del tile
    for r in range(rows_tile):
        rowA = A[i0 + r]
        for c in range(cols_tile):
            # Producto escalar de rowA (0..n) y columna B (0..n) en j0+c
            acc = 0
            first = True
            col_index = j0 + c
            for k in range(n):
                prod = rowA[k] * B[k][col_index]
                mults += 1
                if first:
                    acc = prod
                    first = False
                else:
                    acc += prod
                    adds += 1
            tile[r][c] = acc
    return WorkResultTile(i0, j0, tile, mults, adds)


def multiply_matrices_blocked(A: IntMatrix,
                              B: IntMatrix,
                              workers: int,
                              method: str,
                              block_rows: int,
                              block_cols: int,
                              collect_stats: bool,
                              timing_detail: bool) -> Tuple[IntMatrix, Dict[str, Any]]:
    t0 = time.perf_counter()
    cpu0 = time.process_time()
    m = len(A)
    n = len(A[0])
    p = len(B[0])

    t_prep_s = time.perf_counter()
    # Para esta versión no transponemos B; accesos en columnas serán menos cache-friendly.
    # (Futuro: se podrían transponer bloques de B por secciones.)
    t_prep_e = time.perf_counter()

    # Construimos lista de tiles
    tiles: List[Tuple[int,int,int,int]] = []
    for i0 in range(0, m, block_rows):
        i1 = min(i0 + block_rows, m)
        for j0 in range(0, p, block_cols):
            j1 = min(j0 + block_cols, p)
            tiles.append((i0,i1,j0,j1))

    exec_cls = ProcessPoolExecutor if method == "process" else ThreadPoolExecutor
    futures = []
    partial_tiles: Dict[Tuple[int,int], WorkResultTile] = {}
    total_mult = 0
    total_add = 0

    t_comp_s = time.perf_counter()
    with exec_cls(max_workers=workers) as executor:
        for (i0,i1,j0,j1) in tiles:
            futures.append(executor.submit(_compute_tile, A, B, i0, i1, j0, j1))
        for fut in as_completed(futures):
            wrt = fut.result()
            partial_tiles[(wrt.i0, wrt.j0)] = wrt
            total_mult += wrt.multiplications
            total_add += wrt.additions
    t_comp_e = time.perf_counter()

    t_asm_s = time.perf_counter()
    C: IntMatrix = [[0]*p for _ in range(m)]
    # Insertar cada tile
    # Ordenamos por i0, luego j0
    for (i0,j0) in sorted(partial_tiles.keys()):
        wrt = partial_tiles[(i0,j0)]
        tile = wrt.tile
        for r, row_vals in enumerate(tile):
            target_row = i0 + r
            # Copiar segmento
            C[target_row][j0:j0+len(row_vals)] = row_vals
    t_asm_e = time.perf_counter()

    t1 = time.perf_counter()
    cpu1 = time.process_time()

    meta: Dict[str, Any] = {}
    if collect_stats:
        theoretical_mult = m * n * p
        theoretical_add = m * (n-1) * p if n>0 else 0
        meta = {
            "mode": "blocked",
            "method": method,
            "dimensions": {"A":[m,n],"B":[n,p],"C":[m,p]},
            "workers": workers,
            "blocked": {
                "block_rows": block_rows,
                "block_cols": block_cols,
                "tiles": len(tiles)
            },
            "operations": {
                "multiplications": total_mult,
                "additions": total_add,
                "total": total_mult + total_add,
                "theoretical": {
                    "multiplications": theoretical_mult,
                    "additions": theoretical_add,
                    "total": theoretical_mult + theoretical_add
                }
            },
            "timing": {
                "wall_total": t1 - t0,
                "cpu_time_main": cpu1 - cpu0
            }
        }
        if timing_detail:
            meta["timing"]["phases"] = {
                "prepare": t_prep_e - t_prep_s,
                "dispatch_compute": t_comp_e - t_comp_s,
                "assemble": t_asm_e - t_asm_s
            }
    return C, meta


# -----------------------------
# API principal adaptativa
# -----------------------------

def multiply_matrices(A: IntMatrix,
                      B: IntMatrix,
                      mode: str = "naive",
                      method: str = "process",
                      workers: Optional[int] = None,
                      chunk_size: Optional[int] = None,
                      block_rows: int = 128,
                      block_cols: int = 128,
                      verify: bool = False,
                      collect_stats: bool = True,
                      timing_detail: bool = True) -> Tuple[IntMatrix, Dict[str, Any]]:
    if not A or not B:
        raise ValueError("Matrices vacías")
    m = len(A); n = len(A[0])
    if len(B) != n:
        raise ValueError("Dimensiones incompatibles")
    p = len(B[0])
    if any(len(r)!=n for r in A):
        raise ValueError("A irregular")
    if any(len(r)!=p for r in B):
        raise ValueError("B irregular")
    if workers is None or workers <= 0:
        import os
        workers = os.cpu_count() or 1

    if mode == "naive":
        C, meta = multiply_matrices_naive(A,B,workers,method,chunk_size,collect_stats,timing_detail)
    elif mode == "blocked":
        if block_rows <=0 or block_cols <=0:
            raise ValueError("Block sizes deben ser > 0")
        C, meta = multiply_matrices_blocked(A,B,workers,method,block_rows,block_cols,collect_stats,timing_detail)
    else:
        raise ValueError("Modo inválido")

    if verify:
        t_ver_s = time.perf_counter()
        C_seq = multiply_sequential(A,B)
        ok = (C_seq == C)
        meta["verified_equal_to_sequential"] = ok
        meta.setdefault("timing", {})["verify"] = time.perf_counter() - t_ver_s
        if not ok:
            meta["verification_note"] = "Difiere de secuencial."

    return C, meta


# -----------------------------
# Escalado / Benchmark simple
# -----------------------------

def scaling_run(A: IntMatrix, B: IntMatrix, workers_list: Sequence[int],
                mode: str, method: str,
                block_rows: int, block_cols: int,
                pattern: str,
                repeats: int,
                verify: bool) -> Dict[str, Any]:
    rowsA = len(A); colsA = len(A[0]); colsB = len(B[0])
    results = []
    for w in workers_list:
        times = []
        last_meta = None
        for r in range(repeats):
            _, meta = multiply_matrices(A,B,
                                        mode=mode,
                                        method=method,
                                        workers=w,
                                        block_rows=block_rows,
                                        block_cols=block_cols,
                                        verify=(verify and r==0))
            times.append(meta["timing"]["wall_total"])
            last_meta = meta
        avg = sum(times)/len(times)
        results.append({
            "workers": w,
            "avg_time": avg,
            "runs": times,
            "mode": mode,
            "method": method
        })
        print(f"[SCALING] workers={w} avg={avg:.6f}s times={','.join(f'{t:.4f}' for t in times)}")
    return {
        "scaling": results,
        "dimensions": {"A":[rowsA, colsA], "B":[colsA, colsB], "C":[rowsA, colsB]},
        "mode": mode,
        "method": method,
        "pattern": pattern,
        "block_rows": block_rows,
        "block_cols": block_cols
    }


# -----------------------------
# CLI
# -----------------------------

def parse_args():
    ap = argparse.ArgumentParser(description="Multiplicación concurrente de matrices (enteros).")
    ap.add_argument("--rowsA", type=int, default=200)
    ap.add_argument("--colsA", type=int, default=300)
    ap.add_argument("--colsB", type=int, default=150)
    ap.add_argument("--workers", type=int, default=0, help="0=auto")
    ap.add_argument("--method", choices=["process","thread"], default="process")
    ap.add_argument("--mode", choices=["naive","blocked"], default="naive")
    ap.add_argument("--chunk-size", type=int, default=0, help="Solo modo naive (0=auto)")
    ap.add_argument("--block-rows", type=int, default=128, help="Modo blocked")
    ap.add_argument("--block-cols", type=int, default=128, help="Modo blocked")
    ap.add_argument("--verify", action="store_true")
    ap.add_argument("--patternA", default="random", help="Patrón A (random|sequential|ij|checker|constant|identityA)")
    ap.add_argument("--patternB", default="random", help="Patrón B (random|sequential|ij|checker|constant|identityB)")
    ap.add_argument("--const-valueA", type=int, default=1)
    ap.add_argument("--const-valueB", type=int, default=1)
    ap.add_argument("--seedA", type=int, default=123)
    ap.add_argument("--seedB", type=int, default=456)
    ap.add_argument("--json-out", default="", help="Archivo para exportar meta/benchmark JSON")
    ap.add_argument("--scaling", default="", help="Lista de workers para escalado ej: 1,2,4,8")
    ap.add_argument("--repeats", type=int, default=3, help="Repeticiones por punto en escalado")
    return ap.parse_args()


def main():
    args = parse_args()

    m = args.rowsA
    n = args.colsA
    p = args.colsB

    # Generar matrices
    A = generate_matrix(m, n, pattern=args.patternA, seed=args.seedA, const_value=args.const_valueA)
    B = generate_matrix(n, p, pattern=args.patternB, seed=args.seedB, const_value=args.const_valueB)

    if args.scaling:
        workers_list = [int(x) for x in args.scaling.split(",") if x.strip()]
        scaling_meta = scaling_run(A,B,workers_list,
                                   mode=args.mode,
                                   method=args.method,
                                   block_rows=args.block_rows,
                                   block_cols=args.block_cols,
                                   pattern=f"A:{args.patternA},B:{args.patternB}",
                                   repeats=args.repeats,
                                   verify=args.verify)
        if args.json_out:
            with open(args.json_out,"w",encoding="utf-8") as f:
                json.dump(scaling_meta,f,indent=2)
            print(f"[INFO] Escalado exportado a {args.json_out}")
        return

    chunk_size = args.chunk_size if args.chunk_size > 0 else None
    workers = args.workers if args.workers > 0 else None

    C, meta = multiply_matrices(A,B,
                                mode=args.mode,
                                method=args.method,
                                workers=workers,
                                chunk_size=chunk_size,
                                block_rows=args.block_rows,
                                block_cols=args.block_cols,
                                verify=args.verify)

    # Resumen
    print("=== RESUMEN ===")
    print(f"Dimensiones: A={m}x{n}, B={n}x{p}, C={m}x{p}")
    print(f"Modo: {meta['mode']}  Método: {meta['method']}")
    if meta["mode"] == "naive":
        print(f"Workers: {meta['workers']}  Chunks: {meta['chunks']}  Chunk size: {meta['chunk_size']}")
    else:
        blk = meta["blocked"]
        print(f"Workers: {meta['workers']}  Tiles: {blk['tiles']}  block_rows={blk['block_rows']} block_cols={blk['block_cols']}")
    print(f"Tiempo total (wall): {meta['timing']['wall_total']:.6f}s")
    if "phases" in meta["timing"]:
        for phase,val in meta["timing"]["phases"].items():
            print(f"  - {phase}: {val:.6f}s")
    ops = meta["operations"]
    print(f"Operaciones (conteo) mult={ops['multiplications']} sumas={ops['additions']}")
    print(f"Operaciones teóricas total={ops['theoretical']['total']}")
    if args.verify:
        print(f"Verificación secuencial: {meta.get('verified_equal_to_sequential', False)}")

    if args.json_out:
        with open(args.json_out,"w",encoding="utf-8") as f:
            json.dump(meta,f,indent=2)
        print(f"[INFO] Metadata exportada a {args.json_out}")

    # Mostrar C si es muy pequeña
    if m <= 5 and p <= 5:
        print("Matriz resultado C:")
        for row in C:
            print(row)


if __name__ == "__main__":
    main()