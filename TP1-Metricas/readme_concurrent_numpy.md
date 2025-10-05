# Extensión NumPy (Opcional)

## Archivo: `matrix_concurrent_numpy.py`

Este módulo proporciona una vía alternativa optimizada mediante NumPy para comparar con la implementación pura.

### Modos

1. direct: Usa `C = A @ B` (equivalente a `np.matmul(A,B)` / `np.dot(A,B)`).
2. blocked: Implementación manual por bloques `(ib, jb, kb)` (triple bucle con tiles) sobre arrays NumPy para fines educativos.
3. hybrid-process: Divide la matriz A en rebanadas horizontales y ejecuta `np.dot` por proceso; útil solo si se controla el número de hilos internos del BLAS a 1 para evitar oversubscription.

### Ejemplos

```bash
# Multiplicación directa
python matrix_concurrent_numpy.py --rowsA 1200 --colsA 1400 --colsB 900 --mode direct

# Bloqueado (tiles 128x128)
python matrix_concurrent_numpy.py --rowsA 1200 --colsA 1400 --colsB 900 --mode blocked --block 128

# Híbrido por procesos (fijando hilos BLAS internos a 1)
OMP_NUM_THREADS=1 MKL_NUM_THREADS=1 OPENBLAS_NUM_THREADS=1 \
python matrix_concurrent_numpy.py --rowsA 3000 --colsA 3000 --colsB 3000 \
  --mode hybrid-process --workers 4 --chunks 4

# Exportar JSON de la corrida
python matrix_concurrent_numpy.py --rowsA 800 --colsA 800 --colsB 800 --mode direct --json-out run_numpy.json
```

### Patrones Soportados

Iguales a la versión pura:
- random
- sequential
- ij
- checker
- constant (usar `--const-value`)
- identityA (para A)
- identityB (para B)

### Métricas

Se devuelven tiempos, modo, dimensiones y (cuando procede) parámetros de bloques y/o concurrencia externa.  
No se cuentan operaciones individualmente (el backend BLAS interno ya optimiza fuertemente); sin embargo se provee el número teórico de operaciones:
- multiplicaciones: m * n * p
- sumas: m * (n - 1) * p

### Recomendaciones de Uso

- Para medir escalado de tu implementación pura vs NumPy: ejecuta el script original y este con el mismo patrón y dimensiones.
- Para tamaños medianos (>= 500) `direct` será normalmente el más rápido.
- `blocked` sirve para análisis o ajuste experimental (puede acercarse a `direct` con buenos block sizes si el BLAS no es muy agresivo).
- `hybrid-process` solo es útil si:
  - Ajustas OMP/MKL/OPENBLAS threads = 1
  - El tamaño es suficientemente grande para amortizar el overhead de serialización.

### Futuras Mejoras Opcionales

- Detección automática del número óptimo de bloques (auto-tuning rápido).
- Uso de memoria compartida para resultados parciales en modo híbrido.
- Medición de Gflops = (2*m*n*p - m*p) / tiempo / 1e9.
