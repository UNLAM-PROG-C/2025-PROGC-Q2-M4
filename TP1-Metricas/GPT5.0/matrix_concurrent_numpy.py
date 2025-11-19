#!/usr/bin/env python3
"""
matrix_concurrent_numpy.py
Implementación alternativa usando NumPy para multiplicación de matrices enteras:
- Modos:
  * direct    A @ B
  * blocked    riple bucle tile (i,j,k)
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
  ttern = pattern.lower()
   pattern == "random":
     generará de un tirón con randint
    rn None
   pattern == "sequential":
    rn lambda: np.arange(rows*cols, dtype=np.int64).reshape(rows, cols)
   pattern == "ij":
    + j
    np.arange(rows, dtype=np.int64).reshape(rows, 1)
    np.arange(cols, dtype=np.int64).reshape(1, cols)
    rn lambda: (I + J)
   pattern == "checker":
    np.arange(rows, dtype=np.int64).reshape(rows, 1)
    np.arange(cols, dtype=np.int64).reshape(1, cols)
    rn lambda: ( (I + J) & 1 )
   pattern == "constant":
    rn lambda: np.full((rows, cols), const_value, dtype=np.int64)
   pattern == "identitya" or pattern == "identityb":
    entity (si rectangular se rellena diagonal hasta min(rows, cols))
    build():
      .zeros((rows, cols), dtype=np.int64)
      n(rows, cols)
      range(d), np.arange(d)] = 1
       M
    rn build
  ise ValueError(f"Patrón desconocido: {pattern}")


def generate_matrix(rows: int, cols: int, pattern: str, seed: Optional[int], const_value: int) -> np.ndarray:
   seed is not None:
    andom.seed(seed)
   = make_pattern_function(pattern, rows, cols, const_value)
   pattern == "random":
    ngo moderado para evitar overflow en tests comparativos
    rn np.random.randint(-10, 11, size=(rows, cols), dtype=np.int64)
  turn fn().astype(np.int64, copy=False)


# ---------------------------------------
# Multiplicación directa
# ---------------------------------------

def multiply_direct(A: np.ndarray, B: np.ndarray) -> np.ndarray:
  turn A @ B  # np.matmul


# ---------------------------------------
# Multiplicación bloqueada (i,j,k)
# ---------------------------------------

def multiply_blocked(A: np.ndarray, B: np.ndarray, block: int) -> np.ndarray:
  "
  oqueo sencillo (m,n,p grandes) iterando en tiles cúbicos aproximados.
  ra simplicidad, un solo parámetro block se usa en i, j y k.
  "
   n = A.shape
  , p = B.shape
   n != n2:
    e ValueError("Dimensiones incompatibles")
  = np.zeros((m, p), dtype=np.int64)
  r i0 in range(0, m, block):
     min(i0 + block, m)
    k0 in range(0, n, block):
      in(k0 + block, n)
      bloque A[i0:i1, k0:k1]
      k = A[i0:i1, k0:k1]
       in range(0, p, block):
        (j0 + block, p)
        = B[k0:k1, j0:j1]
        a en C sub-bloque
        ck) shape (ib, kb) dot (B_block) (kb, jb) -> (ib, jb)
         j0:j1] += A_block @ B_block
  turn C


# ---------------------------------------
# Híbrido por procesos
# ---------------------------------------

def _chunk_dot(A_chunk: np.ndarray, B: np.ndarray, start_row: int) -> Tuple[int, np.ndarray]:
  Retorna (fila_inicial, resultado_parcial)
  turn start_row, A_chunk @ B

def multiply_hybrid_process(A: np.ndarray, B: np.ndarray, workers: int, chunks: int) -> np.ndarray:
   n = A.shape
   chunks <= 0:
    ks = workers
  unk_size = math.ceil(m / chunks)
  tures = []
  = np.zeros((m, B.shape[1]), dtype=np.int64)
  th ProcessPoolExecutor(max_workers=workers) as ex:
    start in range(0, m, chunk_size):
      min(start + chunk_size, m)
      a del slice (inevitable sin shared memory)
      e = A[start:end].copy()
      s.append(ex.submit(_chunk_dot, A_slice, B, start))
    fut in as_completed(futures):
      row, partial = fut.result()
      t_row:start_row+partial.shape[0], :] = partial
  turn C


# ---------------------------------------
# Verificación (comparación)
# ---------------------------------------

def verify_equal(C1: np.ndarray, C2: np.ndarray) -> bool:
   C1.shape != C2.shape:
    rn False
  turn np.array_equal(C1, C2)


# ---------------------------------------
# Lógica principal
# ---------------------------------------

def run_multiplication(rowsA: int, colsA: int, colsB: int,
          A: str, patternB: str,
           int, constB: int,
          Optional[int], seedB: Optional[int],
          tr, block: int,
          : int, chunks: int,
           bool, json_out: str) -> None:

  Generar matrices
  gen_s = time.perf_counter()
  = generate_matrix(rowsA, colsA, patternA, seedA, constA)
  = generate_matrix(colsA, colsB, patternB, seedB, constB)
  gen_e = time.perf_counter()

  Multiplicación
  mul_s = time.perf_counter()
   mode == "direct":
    multiply_direct(A, B)
  if mode == "blocked":
    lock <= 0:
      ValueError("Block size debe ser > 0")
    multiply_blocked(A, B, block=block)
  if mode == "hybrid-process":
    orkers <= 0:
       os
      s = os.cpu_count() or 1
    multiply_hybrid_process(A, B, workers=workers, chunks=chunks)
  se:
    e ValueError("Modo inválido")
  mul_e = time.perf_counter()

  Verificación opcional comparando contra direct
  rification_meta = {}
   verify and mode != "direct":
    r_s = time.perf_counter()
    f = multiply_direct(A, B)
     verify_equal(C, C_ref)
    r_e = time.perf_counter()
    fication_meta = {
      ied_equal_to_direct": ok,
      y_time": t_ver_e - t_ver_s
    
    ot ok:
      cation_meta["note"] = "Resultado difiere de la referencia (direct)."

  Métricas
  lt_theoretical = rowsA * colsA * colsB
  d_theoretical = rowsA * (colsA - 1) * colsB if colsA > 0 else 0

  ta: Dict[str, Any] = {
    e": mode,
    ensions": {"A":[rowsA, colsA], "B":[colsA, colsB], "C":[rowsA, colsB]},
    terns": {"A": patternA, "B": patternB},
    eration_time": t_gen_e - t_gen_s,
    tiplication_time": t_mul_e - t_mul_s,
    l_total": (t_mul_e - t_mul_s) + (t_gen_e - t_gen_s),
    rations_theoretical": {
      plications": mult_theoretical,
      ions": add_theoretical,
      ": mult_theoretical + add_theoretical
    
  

   mode == "blocked":
    ["block"] = block
   mode == "hybrid-process":
    ["workers"] = workers
    ["chunks"] = chunks

  ta.update(verification_meta)

  Resumen
  int("=== RESUMEN NUMPY ===")
  int(f"Dimensiones: A={rowsA}x{colsA}, B={colsA}x{colsB}")
  int(f"Modo: {mode}")
   mode == "blocked":
    t(f"Block size: {block}")
   mode == "hybrid-process":
    t(f"Workers externos: {workers}  Chunks: {chunks}")
  int(f"Tiempo generación: {meta['generation_time']:.6f}s")
  int(f"Tiempo multiplicación: {meta['multiplication_time']:.6f}s")
  int(f"Tiempo total: {meta['wall_total']:.6f}s")
   verify and mode != "direct":
    t(f"Verificación contra 'direct': {meta.get('verified_equal_to_direct')} (tiempo {meta.get('verify_time',0):.6f}s)")

   json_out:
     open(json_out, "w", encoding="utf-8") as f:
      ump(meta, f, indent=2)
    t(f"[INFO] Exportado JSON a {json_out}")

  Mostrar C si pequeña
   rowsA <= 5 and colsB <= 5:
    t("Matriz resultado C:")
    t(C)


# ---------------------------------------
# CLI
# ---------------------------------------

def parse_args():
   = argparse.ArgumentParser(description="Multiplicación de matrices con NumPy (modos: direct, blocked, hybrid-process).")
  .add_argument("--rowsA", type=int, default=400)
  .add_argument("--colsA", type=int, default=500)
  .add_argument("--colsB", type=int, default=300)
  .add_argument("--patternA", default="random")
  .add_argument("--patternB", default="random")
  .add_argument("--const-value", type=int, default=1, help="Valor para patrón constant (se usa en ambas si corresponde)")
  .add_argument("--seedA", type=int, default=123)
  .add_argument("--seedB", type=int, default=456)
  .add_argument("--mode", choices=["direct","blocked","hybrid-process"], default="direct")
  .add_argument("--block", type=int, default=128, help="Tamaño de bloque (modo blocked)")
  .add_argument("--workers", type=int, default=0, help="Workers externos (modo hybrid-process)")
  .add_argument("--chunks", type=int, default=0, help="N° de chunks (modo hybrid-process), 0=igual a workers")
  .add_argument("--verify", action="store_true", help="Verificar contra modo direct (si mode != direct)")
  .add_argument("--json-out", default="", help="Archivo para exportar JSON")
  turn ap.parse_args()


def main():
  gs = parse_args()
  n_multiplication(rowsA=args.rowsA,
          rgs.colsA,
          rgs.colsB,
          A=args.patternA,
          B=args.patternB,
          args.const_value,
          args.const_value,
          rgs.seedA,
          rgs.seedB,
          gs.mode,
          rgs.block,
          =args.workers,
          args.chunks,
          args.verify,
          t=args.json_out)


if __name__ == "__main__":
  in()