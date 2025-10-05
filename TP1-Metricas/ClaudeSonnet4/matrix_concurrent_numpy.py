#!/usr/bin/env python3
"""
Multiplicación Concurrente de Matrices - Implementación NumPy

Versión optimizada usando NumPy con diferentes modos de operación.

Autor: Asistente de IA
Versión: 1.0
"""

import argparse
import json
import multiprocessing
import os
import sys
import time
from concurrent.futures import ProcessPoolExecutor
from dataclasses import dataclass, asdict
from typing import List, Dict, Any, Optional, Tuple

try:
    import numpy as np
except ImportError:
    print("Error: NumPy no está instalado. Ejecute: pip install numpy", file=sys.stderr)
    sys.exit(1)


@dataclass
class TimingData:
    """Estructura para almacenar datos de timing detallados."""
    wall_total: float = 0.0
    generation: float = 0.0
    multiplication: float = 0.0
    verification: float = 0.0


@dataclass
class MatrixResult:
    """Resultado completo de una multiplicación de matrices NumPy."""
    matrix: np.ndarray
    timing: TimingData
    metadata: Dict[str, Any]


def validar_dimensiones(rows_a: int, cols_a: int, cols_b: int) -> None:
    """
    Valida que las dimensiones de las matrices sean válidas.
    
    Args:
        rows_a: Filas de matriz A
        cols_a: Columnas de matriz A (debe igualar filas de B)
        cols_b: Columnas de matriz B
        
    Raises:
        ValueError: Si las dimensiones son inválidas
    """
    if rows_a <= 0 or cols_a <= 0 or cols_b <= 0:
        raise ValueError("Todas las dimensiones deben ser positivas")
    
    if rows_a > 100000 or cols_a > 100000 or cols_b > 100000:
        raise ValueError("Dimensiones demasiado grandes (máximo 100,000 por dimensión)")
    
    # Estimar memoria requerida
    memoria_estimada_gb = (rows_a * cols_a + cols_a * cols_b + rows_a * cols_b) * 8 / (1024**3)
    if memoria_estimada_gb > 16:
        print(f"ADVERTENCIA: Memoria estimada ~{memoria_estimada_gb:.1f} GB")


def generar_matriz_numpy(rows: int, cols: int, pattern: str, const_value: int = 1, seed: Optional[int] = None) -> np.ndarray:
    """
    Genera una matriz NumPy con el patrón especificado.
    
    Args:
        rows: Número de filas
        cols: Número de columnas
        pattern: Tipo de patrón
        const_value: Valor constante para patrón 'constant'
        seed: Semilla para patrón 'random'
        
    Returns:
        Matriz NumPy de tipo int64
    """
    if seed is not None and pattern == 'random':
        np.random.seed(seed)
    
    if pattern == 'random':
        return np.random.randint(1, 101, size=(rows, cols), dtype=np.int64)
    
    elif pattern == 'sequential':
        matrix = np.zeros((rows, cols), dtype=np.int64)
        for i in range(rows):
            for j in range(cols):
                matrix[i, j] = i * cols + j + 1
        return matrix
    
    elif pattern == 'ij':
        i_indices, j_indices = np.meshgrid(range(rows), range(cols), indexing='ij')
        return (i_indices + j_indices).astype(np.int64)
    
    elif pattern == 'checker':
        i_indices, j_indices = np.meshgrid(range(rows), range(cols), indexing='ij')
        return ((i_indices + j_indices) % 2).astype(np.int64)
    
    elif pattern == 'constant':
        return np.full((rows, cols), const_value, dtype=np.int64)
    
    elif pattern == 'identity':
        return np.eye(rows, cols, dtype=np.int64)
    
    else:
        raise ValueError(f"Patrón desconocido: {pattern}")


def multiplicar_chunk_horizontal(args: Tuple[np.ndarray, np.ndarray, int, int]) -> np.ndarray:
    """
    Multiplica un chunk horizontal de A por toda la matriz B.
    
    Args:
        args: Tupla con (A_chunk, B, start_row, end_row)
        
    Returns:
        Chunk del resultado
    """
    A_chunk, B, start_row, end_row = args
    return A_chunk @ B


def multiplicar_blocked_numpy(A: np.ndarray, B: np.ndarray, block_size: int = 64) -> np.ndarray:
    """
    Multiplicación por bloques usando NumPy (educativo).
    
    Args:
        A: Matriz A
        B: Matriz B
        block_size: Tamaño de bloque
        
    Returns:
        Matriz resultado
    """
    rows_a, cols_a = A.shape
    cols_b = B.shape[1]
    
    C = np.zeros((rows_a, cols_b), dtype=np.int64)
    
    # Triple bucle por bloques
    for i in range(0, rows_a, block_size):
        for j in range(0, cols_b, block_size):
            for k in range(0, cols_a, block_size):
                # Límites de bloques
                i_end = min(i + block_size, rows_a)
                j_end = min(j + block_size, cols_b)
                k_end = min(k + block_size, cols_a)
                
                # Multiplicación de bloques
                A_block = A[i:i_end, k:k_end]
                B_block = B[k:k_end, j:j_end]
                C[i:i_end, j:j_end] += A_block @ B_block
    
    return C


def multiplicar_hybrid_process(A: np.ndarray, B: np.ndarray, workers: int = 4, chunks: int = 4) -> np.ndarray:
    """
    Multiplicación híbrida dividiendo A en chunks horizontales.
    
    Args:
        A: Matriz A
        B: Matriz B
        workers: Número de procesos
        chunks: Número de chunks horizontales
        
    Returns:
        Matriz resultado
    """
    rows_a = A.shape[0]
    chunk_size = rows_a // chunks
    
    # Preparar argumentos para cada chunk
    args_list = []
    for i in range(chunks):
        start_row = i * chunk_size
        end_row = start_row + chunk_size if i < chunks - 1 else rows_a
        A_chunk = A[start_row:end_row]
        args_list.append((A_chunk, B, start_row, end_row))
    
    # Procesar en paralelo
    with ProcessPoolExecutor(max_workers=workers) as executor:
        chunk_results = list(executor.map(multiplicar_chunk_horizontal, args_list))
    
    # Ensamblar resultado
    return np.vstack(chunk_results)


def multiplicar_matrices_numpy(A: np.ndarray, B: np.ndarray,
                              mode: str = 'direct',
                              block_size: int = 64,
                              workers: int = 4,
                              chunks: int = 4,
                              verify: bool = False) -> MatrixResult:
    """
    Función principal de multiplicación de matrices NumPy.
    
    Args:
        A, B: Matrices NumPy a multiplicar
        mode: 'direct', 'blocked', o 'hybrid-process'
        block_size: Tamaño de bloque para modo blocked
        workers: Número de workers para hybrid-process
        chunks: Número de chunks para hybrid-process
        verify: Si verificar contra multiplicación directa
        
    Returns:
        MatrixResult con resultado y metadata
    """
    start_time = time.perf_counter()
    timing = TimingData()
    
    # Multiplicación según el modo
    mult_start = time.perf_counter()
    
    if mode == 'direct':
        C = A @ B
    elif mode == 'blocked':
        C = multiplicar_blocked_numpy(A, B, block_size)
    elif mode == 'hybrid-process':
        C = multiplicar_hybrid_process(A, B, workers, chunks)
    else:
        raise ValueError(f"Modo desconocido: {mode}")
    
    timing.multiplication = time.perf_counter() - mult_start
    
    # Verificación opcional
    verification_result = {'enabled': verify, 'passed': True}
    if verify and mode != 'direct':
        verify_start = time.perf_counter()
        C_direct = A @ B
        verification_result['passed'] = np.array_equal(C, C_direct)
        timing.verification = time.perf_counter() - verify_start
    
    timing.wall_total = time.perf_counter() - start_time
    
    # Metadata
    metadata = {
        'mode': mode,
        'dimensions': {
            'A': list(A.shape),
            'B': list(B.shape),
            'C': list(C.shape)
        },
        'verification': verification_result,
        'operations_theoretical': {
            'multiplications': A.shape[0] * A.shape[1] * B.shape[1],
            'additions': A.shape[0] * B.shape[1] * (A.shape[1] - 1) if A.shape[1] > 1 else 0,
            'total': A.shape[0] * A.shape[1] * B.shape[1] + (A.shape[0] * B.shape[1] * (A.shape[1] - 1) if A.shape[1] > 1 else 0)
        }
    }
    
    if mode == 'blocked':
        metadata['block_size'] = block_size
    elif mode == 'hybrid-process':
        metadata['workers'] = workers
        metadata['chunks'] = chunks
    
    return MatrixResult(
        matrix=C,
        timing=timing,
        metadata=metadata
    )


def main():
    """Función principal con interfaz CLI."""
    parser = argparse.ArgumentParser(description='Multiplicación Concurrente de Matrices - NumPy')
    
    # Dimensiones
    parser.add_argument('--rowsA', type=int, required=True, help='Filas de matriz A')
    parser.add_argument('--colsA', type=int, required=True, help='Columnas de matriz A')
    parser.add_argument('--colsB', type=int, required=True, help='Columnas de matriz B')
    
    # Configuración
    parser.add_argument('--mode', choices=['direct', 'blocked', 'hybrid-process'], default='direct',
                       help='Modo de multiplicación')
    parser.add_argument('--block', type=int, default=64,
                       help='Tamaño de bloque para modo blocked')
    parser.add_argument('--workers', type=int, default=4,
                       help='Número de workers para hybrid-process')
    parser.add_argument('--chunks', type=int, default=4,
                       help='Número de chunks para hybrid-process')
    
    # Patrones de datos
    parser.add_argument('--patternA', choices=['random', 'sequential', 'ij', 'checker', 'constant', 'identity'],
                       default='random', help='Patrón para matriz A')
    parser.add_argument('--patternB', choices=['random', 'sequential', 'ij', 'checker', 'constant', 'identity'],
                       default='random', help='Patrón para matriz B')
    parser.add_argument('--const-value', type=int, default=1,
                       help='Valor constante para matrices')
    parser.add_argument('--seedA', type=int, help='Semilla para matriz A')
    parser.add_argument('--seedB', type=int, help='Semilla para matriz B')
    
    # Opciones
    parser.add_argument('--verify', action='store_true',
                       help='Verificar contra multiplicación directa')
    parser.add_argument('--json-out', help='Archivo de salida JSON')
    
    args = parser.parse_args()
    
    try:
        # Validar dimensiones
        validar_dimensiones(args.rowsA, args.colsA, args.colsB)
        
        # Advertencia sobre threads de BLAS
        if args.mode == 'hybrid-process':
            threads_vars = ['OMP_NUM_THREADS', 'MKL_NUM_THREADS', 'OPENBLAS_NUM_THREADS']
            for var in threads_vars:
                if os.environ.get(var) != '1':
                    print(f"ADVERTENCIA: Considere configurar {var}=1 para evitar oversubscription")
        
        # Generar matrices
        print("Generando matrices...")
        gen_start = time.perf_counter()
        
        A = generar_matriz_numpy(args.rowsA, args.colsA, args.patternA, 
                                args.const_value, args.seedA)
        B = generar_matriz_numpy(args.colsA, args.colsB, args.patternB, 
                                args.const_value, args.seedB)
        
        gen_time = time.perf_counter() - gen_start
        print(f"Matrices generadas en {gen_time:.10f} segundos")
        
        # Multiplicación
        print(f"\nMultiplicando matrices {args.rowsA}×{args.colsA} × {args.colsA}×{args.colsB}")
        print(f"Modo: {args.mode}")
        
        if args.mode == 'blocked':
            print(f"Tamaño de bloque: {args.block}")
        elif args.mode == 'hybrid-process':
            print(f"Workers: {args.workers}, Chunks: {args.chunks}")
        
        result = multiplicar_matrices_numpy(
            A, B,
            mode=args.mode,
            block_size=args.block,
            workers=args.workers,
            chunks=args.chunks,
            verify=args.verify
        )
        
        # Mostrar resultados
        print(f"\n=== RESULTADOS ===")
        print(f"Tiempo total: {result.timing.wall_total:.10f} segundos")
        print(f"Tiempo multiplicación: {result.timing.multiplication:.10f} segundos")
        if result.timing.generation > 0:
            print(f"Tiempo generación: {gen_time:.10f} segundos")
        if result.timing.verification > 0:
            print(f"Tiempo verificación: {result.timing.verification:.10f} segundos")
        
        print(f"\n=== OPERACIONES TEÓRICAS ===")
        ops = result.metadata['operations_theoretical']
        print(f"Multiplicaciones: {ops['multiplications']:,}")
        print(f"Sumas: {ops['additions']:,}")
        print(f"Total: {ops['total']:,}")
        
        if args.verify:
            status = "✓ CORRECTA" if result.metadata['verification']['passed'] else "✗ ERROR"
            print(f"\nVerificación: {status}")
        
        # Exportar JSON si se solicita
        if args.json_out:
            export_data = {
                **result.metadata,
                'patterns': {
                    'A': args.patternA,
                    'B': args.patternB
                },
                'generation_time': gen_time,
                'multiplication_time': result.timing.multiplication,
                'wall_total': result.timing.wall_total
            }
            
            with open(args.json_out, 'w') as f:
                json.dump(export_data, f, indent=2)
            print(f"\nMetadata exportada a {args.json_out}")
    
    except ValueError as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)
    except KeyboardInterrupt:
        print("\nOperación cancelada por el usuario")
        sys.exit(1)
    except Exception as e:
        print(f"Error inesperado: {e}", file=sys.stderr)
        sys.exit(1)


if __name__ == '__main__':
    main()