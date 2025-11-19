# Multiplicación Concurrente de Matrices (Enteros, Python Stdlib)

## Objetivos
Implementar multiplicación de matrices enteras A(m×n) · B(n×p) con:
- Concurrencia con hilos o procesos (`thread` / `process`)
- Modo naive (por filas) y modo bloqueado (tiling) para locality
- Benchmark detallado (tiempos totales y por fases)
- Conteo de operaciones (multiplicaciones y sumas reales vs teóricas)
- Exportación de métricas a JSON
- Verificación secuencial opcional
- Generación de datos determinista reproducible (diversos patrones)
- Escalado comparativo (workers)

## Advertencia sobre Tamaños Extremadamente Grandes
Un tamaño 100000×100000 (1e10 elementos) es impracticable en Python puro:
- Memoria: cada entero Python ocupa decenas de bytes (decenas de GB).
- Tiempo: O(m·n·p) se vuelve inabordable (>= 1e15 operaciones).
Se recomiendan:
- Ensayos de hasta unos pocos miles por dimensión
- Patrones deterministas + verificación parcial
- (Futuro) Representaciones en memoria compartida o librerías especializadas (NumPy / librerías C)

## Modos
- naive: divide el trabajo por filas (chunks), transpone B una vez.
- blocked: divide C en tiles (bloques de filas y columnas). Cada tile se calcula completo de una sola pasada (k entero). Mejora locality y permite controlar granularidad.

## Patrones de Generación
`--pattern`:
- random: enteros pseudoaleatorios reproducibles (seed)
- sequential: A[i][j] = i*n + j
- ij: A[i][j] = i + j
- checker: (i + j) % 2
- constant: usa --const-value
- identityA: si es cuadrada; filas no diagonales = 0, diagonal = 1 (para A)
- identityB: idem para B (solo válido si B es cuadrada vista como n×p con n==p)

## Ejemplos

### Multiplicación naive con verificación
```bash
python matrix_concurrent.py --rowsA 600 --colsA 800 --colsB 500 \
  --workers 8 --method process --verify --pattern sequential
```

### Modo bloqueado
```bash
python matrix_concurrent.py --rowsA 1200 --colsA 1200 --colsB 1200 \
  --workers 8 --method process --mode blocked --block-rows 96 --block-cols 96
```

### Benchmark de escalado
```bash
python matrix_concurrent.py --rowsA 400 --colsA 400 --colsB 400 \
  --scaling 1,2,4,8 --mode naive --pattern ij --json-out scaling.json
```

### Exportar metadata a JSON
```bash
python matrix_concurrent.py --rowsA 300 --colsA 400 --colsB 250 \
  --json-out run_meta.json
```

## Salida JSON (ejemplo resumido)
```json
{
  "mode": "blocked",
  "method": "process",
  "dimensions": {"A": [300,400], "B": [400,250], "C": [300,250]},
  "pattern": "sequential",
  "operations": {...},
  "timing": {
  all_total": 0.8421,
  pu_time_main": 0.6032,
  hases": {...}
  },
  "blocked": {
  lock_rows": 96,
  lock_cols": 96,
  iles": 12
  },
  "verified_equal_to_sequential": true
}
```

## Estrategia Interna (Modo Blocked)
Para cada tile (i0:i1, j0:j1) se calcula:
C_tile[r][c] = sum_{k=0..n-1} A[i0+r][k] * B[k][j0+c]
Cada tile es independiente ⇒ se paraleliza sobre el conjunto de tiles.  
Esto evita tener que hacer acumulaciones intermedias, a costa de volver a recorrer la dimensión interna completa por cada tile.

## Optimización Futura Recomendada
1. Bloqueo triple (i,j,k) con acumulación parcial para n enorme.
2. Uso de memoria compartida (`multiprocessing.shared_memory`) para B (y quizá A).
3. Vectorización en C (extensión) o migrar a NumPy solo para comparación.
4. Detección adaptativa de block size (benchmark interno rápido).
5. Lazy / fórmula para B en modo identity evitando almacenar.

## Limitaciones
- Python puro implica overhead por elemento; el speedup se ve mejor en matrices medianas (p.e. 400–1500).
- Para hilos, el GIL limita CPU; usar `process` para trabajo intensivo.

## Escalado
Usa `--scaling w1,w2,...` para medir tiempos y generar tabla+JSON (si se pasa `--json-out`).

## Contacto para Ajustes
Puedes pedirme:
- Añadir memoria compartida
- Añadir modo parcial (multiplicar por bloques serialmente pero liberando intermedios)
- Añadir sampling parcial para verificación en lugar de verificación completa
- Añadir pruebas automatizadas
