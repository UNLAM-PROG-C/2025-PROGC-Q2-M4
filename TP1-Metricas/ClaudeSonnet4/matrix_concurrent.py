#!/usr/bin/env python3
"""
Multiplicación Concurrente de Matrices - Implementación Python Puro

Implementa multiplicación de matrices con soporte para concurrencia,
múltiples algoritmos y benchmarking detallado.

Autor: Asistente de IA
Versión: 1.0
"""

import argparse
import json
import multiprocessing
import os
import random
import sys
import time
from concurrent.futures import ProcessPoolExecutor, ThreadPoolExecutor
from dataclasses import dataclass, asdict
from typing import List, Dict, Any, Optional, Tuple, Union
import threading


@dataclass
class TimingData:
    """Estructura para almacenar datos de timing detallados."""
    wall_total: float = 0.0
    cpu_time_main: float = 0.0
    generation: float = 0.0
    preparation: float = 0.0
    computation: float = 0.0
    assembly: float = 0.0
    verification: float = 0.0


@dataclass
class OperationCount:
    """Contador de operaciones realizadas."""
    multiplications_real: int = 0
    additions_real: int = 0
    total_real: int = 0
    theoretical: Dict[str, int] = None

    def __post_init__(self):
        if self.theoretical is None:
            self.theoretical = {}


@dataclass
class MatrixResult:
    """Resultado completo de una multiplicación de matrices."""
    matrix: List[List[int]]
    timing: TimingData
    operations: OperationCount
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
    
    if rows_a > 50000 or cols_a > 50000 or cols_b > 50000:
        raise ValueError("Dimensiones demasiado grandes (máximo 50,000 por dimensión)")
    
    # Estimar memoria requerida (aproximadamente)
    memoria_estimada_gb = (rows_a * cols_a + cols_a * cols_b + rows_a * cols_b) * 24 / (1024**3)
    if memoria_estimada_gb > 8:
        print(f"ADVERTENCIA: Memoria estimada ~{memoria_estimada_gb:.1f} GB")


def generar_matriz(rows: int, cols: int, pattern: str, const_value: int = 1, seed: Optional[int] = None) -> List[List[int]]:
    """
    Genera una matriz con el patrón especificado.
    
    Args:
        rows: Número de filas
        cols: Número de columnas
        pattern: Tipo de patrón ('random', 'sequential', 'ij', 'checker', 'constant', 'identity')
        const_value: Valor constante para patrón 'constant'
        seed: Semilla para patrón 'random'
        
    Returns:
        Matriz generada como lista de listas
    """
    if seed is not None and pattern == 'random':
        random.seed(seed)
    
    matrix = [[0 for _ in range(cols)] for _ in range(rows)]
    
    if pattern == 'random':
        for i in range(rows):
            for j in range(cols):
                matrix[i][j] = random.randint(1, 100)
    
    elif pattern == 'sequential':
        for i in range(rows):
            for j in range(cols):
                matrix[i][j] = i * cols + j + 1
    
    elif pattern == 'ij':
        for i in range(rows):
            for j in range(cols):
                matrix[i][j] = i + j
    
    elif pattern == 'checker':
        for i in range(rows):
            for j in range(cols):
                matrix[i][j] = (i + j) % 2
    
    elif pattern == 'constant':
        for i in range(rows):
            for j in range(cols):
                matrix[i][j] = const_value
    
    elif pattern == 'identity':
        for i in range(rows):
            for j in range(cols):
                if i == j:
                    matrix[i][j] = 1
                else:
                    matrix[i][j] = 0
    
    else:
        raise ValueError(f"Patrón desconocido: {pattern}")
    
    return matrix


def transponer_matriz(matrix: List[List[int]]) -> List[List[int]]:
    """Transpone una matriz para mejorar localidad de memoria."""
    rows = len(matrix)
    cols = len(matrix[0])
    transposed = [[0 for _ in range(rows)] for _ in range(cols)]
    
    for i in range(rows):
        for j in range(cols):
            transposed[j][i] = matrix[i][j]
    
    return transposed


def multiplicar_fila_naive(args: Tuple[List[List[int]], List[List[int]], int, int, int, int]) -> Tuple[int, int, List[int]]:
    """
    Multiplica una fila de A por toda la matriz B transpuesta.
    
    Args:
        args: Tupla con (A, B_T, row_start, row_end, cols_a, cols_b)
        
    Returns:
        Tupla con (multiplicaciones, sumas, filas_resultado)
    """
    A, B_T, row_start, row_end, cols_a, cols_b = args
    
    multiplicaciones = 0
    sumas = 0
    filas_resultado = []
    
    for i in range(row_start, row_end):
        fila_resultado = []
        for j in range(cols_b):
            suma = 0
            for k in range(cols_a):
                producto = A[i][k] * B_T[j][k]
                suma += producto
                multiplicaciones += 1
                if k > 0:
                    sumas += 1
            fila_resultado.append(suma)
        filas_resultado.append(fila_resultado)
    
    return multiplicaciones, sumas, filas_resultado


def multiplicar_bloque(args: Tuple[List[List[int]], List[List[int]], int, int, int, int, int, int]) -> Tuple[int, int, List[Tuple[int, int, int]]]:
    """
    Multiplica un bloque de la matriz C.
    
    Args:
        args: Tupla con (A, B, i_start, i_end, j_start, j_end, cols_a, resultado_shape)
        
    Returns:
        Tupla con (multiplicaciones, sumas, elementos_resultado)
    """
    A, B, i_start, i_end, j_start, j_end, cols_a, _ = args
    
    multiplicaciones = 0
    sumas = 0
    elementos = []
    
    for i in range(i_start, i_end):
        for j in range(j_start, j_end):
            suma = 0
            for k in range(cols_a):
                producto = A[i][k] * B[k][j]
                suma += producto
                multiplicaciones += 1
                if k > 0:
                    sumas += 1
            elementos.append((i, j, suma))
    
    return multiplicaciones, sumas, elementos


def multiplicar_secuencial(A: List[List[int]], B: List[List[int]]) -> Tuple[List[List[int]], OperationCount]:
    """
    Multiplicación secuencial para verificación.
    
    Args:
        A: Matriz A
        B: Matriz B
        
    Returns:
        Tupla con (matriz_resultado, contador_operaciones)
    """
    rows_a = len(A)
    cols_a = len(A[0])
    cols_b = len(B[0])
    
    C = [[0 for _ in range(cols_b)] for _ in range(rows_a)]
    
    multiplicaciones = 0
    sumas = 0
    
    for i in range(rows_a):
        for j in range(cols_b):
            for k in range(cols_a):
                producto = A[i][k] * B[k][j]
                C[i][j] += producto
                multiplicaciones += 1
                if k > 0:
                    sumas += 1
    
    operations = OperationCount(
        multiplications_real=multiplicaciones,
        additions_real=sumas,
        total_real=multiplicaciones + sumas,
        theoretical={
            'multiplications': rows_a * cols_b * cols_a,
            'additions': rows_a * cols_b * (cols_a - 1) if cols_a > 1 else 0,
            'total': rows_a * cols_b * cols_a + (rows_a * cols_b * (cols_a - 1) if cols_a > 1 else 0)
        }
    )
    
    return C, operations


def multiplicar_matrices(A: List[List[int]], B: List[List[int]], 
                        mode: str = 'naive', method: str = 'process',
                        workers: Optional[int] = None, chunk_size: Optional[int] = None,
                        block_rows: int = 64, block_cols: int = 64,
                        verify: bool = False, collect_stats: bool = True,
                        timing_detail: bool = True) -> MatrixResult:
    """
    Función principal de multiplicación de matrices.
    
    Args:
        A, B: Matrices a multiplicar
        mode: 'naive' o 'blocked'
        method: 'process' o 'thread'
        workers: Número de workers (None = automático)
        chunk_size: Tamaño de chunk para modo naive
        block_rows, block_cols: Dimensiones de bloques para modo blocked
        verify: Si verificar contra implementación secuencial
        collect_stats: Si recolectar estadísticas detalladas
        timing_detail: Si medir tiempos por fase
        
    Returns:
        MatrixResult con resultado y metadata
    """
    rows_a = len(A)
    cols_a = len(A[0])
    cols_b = len(B[0])
    
    start_time = time.perf_counter()
    start_cpu = time.process_time()
    
    timing = TimingData()
    
    # Auto-configurar workers
    if workers is None or workers == 0:
        workers = min(multiprocessing.cpu_count(), max(1, rows_a // 50))
    
    # Auto-configurar chunk_size
    if chunk_size is None:
        chunk_size = max(1, rows_a // workers)
    
    # Preparación
    prep_start = time.perf_counter()
    
    if mode == 'naive':
        B_T = transponer_matriz(B)
        chunks = []
        for i in range(0, rows_a, chunk_size):
            end_i = min(i + chunk_size, rows_a)
            chunks.append((A, B_T, i, end_i, cols_a, cols_b))
    
    elif mode == 'blocked':
        chunks = []
        for i in range(0, rows_a, block_rows):
            for j in range(0, cols_b, block_cols):
                end_i = min(i + block_rows, rows_a)
                end_j = min(j + block_cols, cols_b)
                chunks.append((A, B, i, end_i, j, end_j, cols_a, (rows_a, cols_b)))
    
    else:
        raise ValueError(f"Modo desconocido: {mode}")
    
    timing.preparation = time.perf_counter() - prep_start
    
    # Computación
    comp_start = time.perf_counter()
    
    total_multiplicaciones = 0
    total_sumas = 0
    
    ExecutorClass = ProcessPoolExecutor if method == 'process' else ThreadPoolExecutor
    
    if method == 'process':
        with ExecutorClass(max_workers=workers) as executor:
            if mode == 'naive':
                results = list(executor.map(multiplicar_fila_naive, chunks))
            else:  # blocked
                results = list(executor.map(multiplicar_bloque, chunks))
    else:  # thread
        with ExecutorClass(max_workers=workers) as executor:
            if mode == 'naive':
                results = list(executor.map(multiplicar_fila_naive, chunks))
            else:  # blocked
                results = list(executor.map(multiplicar_bloque, chunks))
    
    timing.computation = time.perf_counter() - comp_start
    
    # Ensamblado
    assembly_start = time.perf_counter()
    
    C = [[0 for _ in range(cols_b)] for _ in range(rows_a)]
    
    if mode == 'naive':
        row_idx = 0
        for mult_count, sum_count, filas in results:
            total_multiplicaciones += mult_count
            total_sumas += sum_count
            for fila in filas:
                C[row_idx] = fila
                row_idx += 1
    
    elif mode == 'blocked':
        for mult_count, sum_count, elementos in results:
            total_multiplicaciones += mult_count
            total_sumas += sum_count
            for i, j, valor in elementos:
                C[i][j] = valor
    
    timing.assembly = time.perf_counter() - assembly_start
    
    # Verificación opcional
    verification_result = {'enabled': verify, 'passed': True}
    if verify:
        verify_start = time.perf_counter()
        C_seq, _ = multiplicar_secuencial(A, B)
        
        # Comparar matrices
        matrices_iguales = True
        for i in range(rows_a):
            for j in range(cols_b):
                if C[i][j] != C_seq[i][j]:
                    matrices_iguales = False
                    break
            if not matrices_iguales:
                break
        
        verification_result['passed'] = matrices_iguales
        timing.verification = time.perf_counter() - verify_start
    
    # Timing total
    timing.wall_total = time.perf_counter() - start_time
    timing.cpu_time_main = time.process_time() - start_cpu
    
    # Operaciones
    operations = OperationCount(
        multiplications_real=total_multiplicaciones,
        additions_real=total_sumas,
        total_real=total_multiplicaciones + total_sumas,
        theoretical={
            'multiplications': rows_a * cols_b * cols_a,
            'additions': rows_a * cols_b * (cols_a - 1) if cols_a > 1 else 0,
            'total': rows_a * cols_b * cols_a + (rows_a * cols_b * (cols_a - 1) if cols_a > 1 else 0)
        }
    )
    
    # Metadata
    metadata = {
        'mode': mode,
        'method': method,
        'workers': workers,
        'dimensions': {
            'A': [rows_a, cols_a],
            'B': [cols_a, cols_b],
            'C': [rows_a, cols_b]
        },
        'verification': verification_result
    }
    
    if mode == 'naive':
        metadata['chunk_size'] = chunk_size
    else:
        metadata['block_rows'] = block_rows
        metadata['block_cols'] = block_cols
    
    return MatrixResult(
        matrix=C,
        timing=timing,
        operations=operations,
        metadata=metadata
    )


def benchmark_escalado(rows_a: int, cols_a: int, cols_b: int,
                      workers_list: List[int], repeats: int = 3,
                      pattern_a: str = 'random', pattern_b: str = 'random',
                      seed_a: Optional[int] = None, seed_b: Optional[int] = None,
                      **kwargs) -> Dict[str, Any]:
    """
    Ejecuta benchmark de escalado con diferentes números de workers.
    
    Args:
        rows_a, cols_a, cols_b: Dimensiones de matrices
        workers_list: Lista de números de workers a probar
        repeats: Número de repeticiones por configuración
        pattern_a, pattern_b: Patrones de generación
        seed_a, seed_b: Semillas para reproducibilidad
        **kwargs: Argumentos adicionales para multiplicar_matrices
        
    Returns:
        Diccionario con resultados de benchmark
    """
    print(f"Iniciando benchmark de escalado: {rows_a}×{cols_a} × {cols_a}×{cols_b}")
    print(f"Workers: {workers_list}, Repeticiones: {repeats}")
    
    # Generar matrices una sola vez
    gen_start = time.perf_counter()
    A = generar_matriz(rows_a, cols_a, pattern_a, seed=seed_a)
    B = generar_matriz(cols_a, cols_b, pattern_b, seed=seed_b)
    gen_time = time.perf_counter() - gen_start
    
    resultados = {
        'dimensions': {'A': [rows_a, cols_a], 'B': [cols_a, cols_b]},
        'patterns': {'A': pattern_a, 'B': pattern_b},
        'generation_time': gen_time,
        'repeats': repeats,
        'results': {}
    }
    
    for workers in workers_list:
        print(f"\nProbando con {workers} workers...")
        tiempos = []
        
        for rep in range(repeats):
            result = multiplicar_matrices(A, B, workers=workers, verify=False, **kwargs)
            tiempos.append(result.timing.wall_total)
            print(f"  Repetición {rep+1}: {result.timing.wall_total:.4f}s")
        
        tiempo_promedio = sum(tiempos) / len(tiempos)
        tiempo_min = min(tiempos)
        tiempo_max = max(tiempos)
        
        resultados['results'][str(workers)] = {
            'times': tiempos,
            'average': tiempo_promedio,
            'min': tiempo_min,
            'max': tiempo_max,
            'operations': asdict(result.operations)
        }
        
        print(f"  Promedio: {tiempo_promedio:.4f}s")
    
    return resultados


def main():
    """Función principal con interfaz CLI."""
    parser = argparse.ArgumentParser(description='Multiplicación Concurrente de Matrices - Python Puro')
    
    # Dimensiones
    parser.add_argument('--rowsA', type=int, required=True, help='Filas de matriz A')
    parser.add_argument('--colsA', type=int, required=True, help='Columnas de matriz A')
    parser.add_argument('--colsB', type=int, required=True, help='Columnas de matriz B')
    
    # Configuración de algoritmo
    parser.add_argument('--mode', choices=['naive', 'blocked'], default='naive',
                       help='Modo de multiplicación')
    parser.add_argument('--method', choices=['process', 'thread'], default='process',
                       help='Método de concurrencia')
    parser.add_argument('--workers', type=int, default=0,
                       help='Número de workers (0=automático)')
    
    # Parámetros específicos
    parser.add_argument('--chunk-size', type=int,
                       help='Tamaño de chunk para modo naive')
    parser.add_argument('--block-rows', type=int, default=64,
                       help='Filas por bloque para modo blocked')
    parser.add_argument('--block-cols', type=int, default=64,
                       help='Columnas por bloque para modo blocked')
    
    # Patrones de datos
    parser.add_argument('--patternA', choices=['random', 'sequential', 'ij', 'checker', 'constant', 'identity'],
                       default='random', help='Patrón para matriz A')
    parser.add_argument('--patternB', choices=['random', 'sequential', 'ij', 'checker', 'constant', 'identity'],
                       default='random', help='Patrón para matriz B')
    parser.add_argument('--const-valueA', type=int, default=1,
                       help='Valor constante para matriz A')
    parser.add_argument('--const-valueB', type=int, default=1,
                       help='Valor constante para matriz B')
    parser.add_argument('--seedA', type=int, help='Semilla para matriz A')
    parser.add_argument('--seedB', type=int, help='Semilla para matriz B')
    
    # Opciones
    parser.add_argument('--verify', action='store_true',
                       help='Verificar contra implementación secuencial')
    parser.add_argument('--json-out', help='Archivo de salida JSON')
    
    # Benchmark de escalado
    parser.add_argument('--scaling', help='Lista de workers para benchmark (ej: 1,2,4,8)')
    parser.add_argument('--repeats', type=int, default=3,
                       help='Repeticiones para benchmark de escalado')
    
    args = parser.parse_args()
    
    try:
        # Validar dimensiones
        validar_dimensiones(args.rowsA, args.colsA, args.colsB)
        
        # Generar matrices
        print("Generando matrices...")
        gen_start = time.perf_counter()
        
        A = generar_matriz(args.rowsA, args.colsA, args.patternA, 
                          args.const_valueA, args.seedA)
        B = generar_matriz(args.colsA, args.colsB, args.patternB, 
                          args.const_valueB, args.seedB)
        
        gen_time = time.perf_counter() - gen_start
        print(f"Matrices generadas en {gen_time:.6f} segundos")
        
        if args.scaling:
            # Benchmark de escalado
            workers_list = [int(x.strip()) for x in args.scaling.split(',')]
            
            kwargs = {
                'mode': args.mode,
                'method': args.method,
                'chunk_size': getattr(args, 'chunk_size', None),
                'block_rows': args.block_rows,
                'block_cols': args.block_cols
            }
            
            resultados = benchmark_escalado(
                args.rowsA, args.colsA, args.colsB,
                workers_list, args.repeats,
                args.patternA, args.patternB,
                args.seedA, args.seedB,
                **kwargs
            )
            
            if args.json_out:
                with open(args.json_out, 'w') as f:
                    json.dump(resultados, f, indent=2)
                print(f"\nResultados exportados a {args.json_out}")
            
        else:
            # Multiplicación simple
            print(f"\nMultiplicando matrices {args.rowsA}×{args.colsA} × {args.colsA}×{args.colsB}")
            print(f"Modo: {args.mode}, Método: {args.method}, Workers: {args.workers or 'auto'}")
            
            result = multiplicar_matrices(
                A, B,
                mode=args.mode,
                method=args.method,
                workers=args.workers if args.workers > 0 else None,
                chunk_size=getattr(args, 'chunk_size', None),
                block_rows=args.block_rows,
                block_cols=args.block_cols,
                verify=args.verify
            )
            
            # Mostrar resultados
            print(f"\n=== RESULTADOS ===")
            print(f"Tiempo total: {result.timing.wall_total:.10f} segundos")
            print(f"Tiempo CPU principal: {result.timing.cpu_time_main:.10f} segundos")
            
            if result.timing.generation > 0:
                print(f"Generación: {result.timing.generation:.10f}s")
            print(f"Preparación: {result.timing.preparation:.10f}s")
            print(f"Computación: {result.timing.computation:.10f}s")
            print(f"Ensamblado: {result.timing.assembly:.10f}s")
            if result.timing.verification > 0:
                print(f"Verificación: {result.timing.verification:.10f}s")
            
            print(f"\n=== OPERACIONES ===")
            print(f"Multiplicaciones: {result.operations.multiplications_real:,}")
            print(f"Sumas: {result.operations.additions_real:,}")
            print(f"Total: {result.operations.total_real:,}")
            print(f"Teóricas: {result.operations.theoretical['total']:,}")
            
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
                    'operations': asdict(result.operations),
                    'timing': asdict(result.timing)
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