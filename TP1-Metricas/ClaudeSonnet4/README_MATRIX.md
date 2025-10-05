# Multiplicación Concurrente de Matrices

Este proyecto implementa algoritmos de multiplicación de matrices con soporte para concurrencia, benchmarking detallado y múltiples patrones de generación de datos.

## Características Principales

- **Dos implementaciones**: Python puro y NumPy optimizado
- **Concurrencia**: Soporte para threads y processes
- **Modos de multiplicación**: Naive, blocked y hybrid-process
- **Patrones de datos**: Random, sequential, identity, checker, etc.
- **Benchmarking**: Timing detallado con exportación JSON
- **Verificación**: Comparación opcional contra implementación secuencial
- **Solo enteros**: Trabajo exclusivo con números enteros

## Instalación

```bash
# Solo se requiere Python 3.7+ para la versión pura
# Para la versión NumPy:
pip install numpy
```

## Uso Básico

### Versión Python Puro

```bash
# Multiplicación básica 600x800 * 800x500
python matrix_concurrent.py --rowsA 600 --colsA 800 --colsB 500 --workers 8 --method process --mode naive --verify --patternA sequential --patternB ij

# Modo blocked con tiles optimizados
python matrix_concurrent.py --rowsA 1200 --colsA 1200 --colsB 1200 --mode blocked --block-rows 96 --block-cols 96 --workers 8 --method process

# Benchmark de escalado
python matrix_concurrent.py --rowsA 400 --colsA 400 --colsB 400 --scaling 1,2,4,8 --mode naive --patternA ij --patternB checker --json-out scaling.json
```

### Versión NumPy

```bash
# Multiplicación directa (optimizada)
python matrix_concurrent_numpy.py --rowsA 1200 --colsA 1400 --colsB 900 --mode direct

# Modo hybrid-process (evitar oversubscription)
OMP_NUM_THREADS=1 MKL_NUM_THREADS=1 OPENBLAS_NUM_THREADS=1 python matrix_concurrent_numpy.py --rowsA 3000 --colsA 3000 --colsB 3000 --mode hybrid-process --workers 4 --chunks 4
```

## Parámetros Principales

### Versión Pura (matrix_concurrent.py)

- `--rowsA`, `--colsA`, `--colsB`: Dimensiones de las matrices
- `--workers`: Número de workers (0=automático)
- `--method`: `process` o `thread`
- `--mode`: `naive` o `blocked`
- `--chunk-size`: Tamaño de chunk para modo naive
- `--block-rows`, `--block-cols`: Dimensiones de tiles para modo blocked
- `--verify`: Verificar contra implementación secuencial
- `--patternA`, `--patternB`: Patrón de generación de datos
- `--json-out`: Archivo de salida JSON

### Versión NumPy (matrix_concurrent_numpy.py)

- `--mode`: `direct`, `blocked`, o `hybrid-process`
- `--block`: Tamaño de bloque para modo blocked
- `--chunks`: Número de chunks horizontales para hybrid-process
- `--verify`: Verificar contra multiplicación directa

## Patrones de Generación

- `random`: Valores aleatorios (requiere seed)
- `sequential`: A[i][j] = i*cols + j + 1
- `ij`: A[i][j] = i + j
- `checker`: A[i][j] = (i + j) % 2
- `constant`: Valor constante configurable
- `identityA`/`identityB`: Matriz identidad (o pseudo-identidad)

## Ejemplo de Salida JSON

```json
{
  "mode": "naive",
  "method": "process",
  "dimensions": {
    "A": [600, 800],
    "B": [800, 500],
    "C": [600, 500]
  },
  "workers": 8,
  "chunk_size": 75,
  "patterns": {
    "A": "sequential",
    "B": "ij"
  },
  "operations": {
    "multiplications_real": 240000000,
    "additions_real": 239400000,
    "total_real": 479400000,
    "theoretical": {
      "multiplications": 240000000,
      "additions": 239400000,
      "total": 479400000
    }
  },
  "timing": {
    "wall_total": 15.2847392000,
    "cpu_time_main": 14.8923445000,
    "phases": {
      "generation": 0.1234567890,
      "preparation": 0.0987654321,
      "computation": 14.9876543210,
      "assembly": 0.0749888777
    }
  },
  "verification": {
    "enabled": true,
    "passed": true
  }
}
```

## Advertencias y Limitaciones

### Limitaciones de Memoria

- **Matrices grandes (>10,000x10,000)**: Pueden exceder la memoria disponible
- **Cálculo estimado**: Memoria ≈ 3 × m × n × p × 24 bytes (Python ints)
- **Ejemplo**: 5000×5000×5000 ≈ 9 GB de RAM

### Rendimiento

- **Threads vs Processes**: Los threads no aceleran operaciones CPU-intensivas debido al GIL
- **Serialización**: Los processes tienen overhead de serialización de datos
- **Tamaños mínimos**: Matrices muy pequeñas (<100×100) pueden ser más lentas con concurrencia

### Recomendaciones

1. **Para matrices pequeñas (<1000×1000)**: Usar modo secuencial
2. **Para matrices medianas (1000-5000)**: Modo naive con processes
3. **Para matrices grandes (>5000)**: Modo blocked con tiles optimizados
4. **NumPy**: Preferir modo direct para rendimiento máximo

## Ejemplos Avanzados

### Benchmark Completo

```bash
# Comparar diferentes configuraciones
python matrix_concurrent.py --rowsA 1000 --colsA 1000 --colsB 1000 \
  --scaling 1,2,4,8,16 --repeats 5 --mode naive --method process \
  --patternA random --patternB random --seedA 42 --seedB 84 \
  --json-out benchmark_naive.json

python matrix_concurrent.py --rowsA 1000 --colsA 1000 --colsB 1000 \
  --scaling 1,2,4,8,16 --repeats 5 --mode blocked \
  --block-rows 64 --block-cols 64 --method process \
  --json-out benchmark_blocked.json
```

### Verificación de Exactitud

```bash
# Verificar con patrones deterministas
python matrix_concurrent.py --rowsA 500 --colsA 600 --colsB 400 \
  --mode naive --method process --workers 4 --verify \
  --patternA sequential --patternB checker
```

### Comparación NumPy vs Puro

```bash
# NumPy optimizado
time python matrix_concurrent_numpy.py --rowsA 2000 --colsA 2000 --colsB 2000 --mode direct

# Python puro equivalent
time python matrix_concurrent.py --rowsA 2000 --colsA 2000 --colsB 2000 --mode blocked --workers 8
```

## Mejoras Futuras

- **Memoria compartida**: Reducir overhead de serialización
- **Triple blocking**: Optimización de cache con tiling 3D (i,j,k)
- **Auto-tuning**: Selección automática de parámetros óptimos
- **Verificación parcial**: Sampling para matrices muy grandes
- **Medición de FLOPS**: Análisis de rendimiento en operaciones por segundo