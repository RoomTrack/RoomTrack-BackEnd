# language: es
@Sprint4 @US24
Característica: US-24 — Reporte de productividad del personal
  Como administrador del hotel
  quiero ver cuántas tareas completó cada colaborador y comparar las áreas
  para distribuir mejor la carga de trabajo y reconocer al personal.
  Ejecutados con Reqnroll contra los casos de uso, repositorios y consultas reales del servicio Accommodations
  (base de datos en memoria): el reporte se calcula con las limpiezas, incidencias y solicitudes realmente terminadas.

  Antecedentes:
    Dado un hotel con la habitación 101 y la habitación 102
    Y "Ana" trabaja en limpieza
    Y "Rosa" trabaja en limpieza
    Y "Jorge" trabaja en mantenimiento

  @US24_E01
  Escenario: Tareas completadas por colaborador
    Dado que "Ana" completó 2 limpiezas
    Y que "Rosa" completó 1 limpieza
    Y que "Jorge" resolvió 1 incidencia
    Cuando consulto el reporte de productividad
    Entonces el reporte muestra que "Ana" completó 2 tareas
    Y el reporte muestra que "Rosa" completó 1 tarea
    Y el reporte muestra que "Jorge" completó 1 tarea
    Y "Ana" aparece primero en el reporte

  @US24_E02
  Escenario: Comparación entre áreas
    Dado que "Ana" completó 2 limpiezas
    Y que "Rosa" completó 1 limpieza
    Y que "Jorge" resolvió 1 incidencia
    Cuando consulto el reporte de productividad
    Entonces el área de limpieza completó 3 tareas con 2 colaboradores
    Y el área de mantenimiento completó 1 tarea con 1 colaborador
    Y el área de recepción aparece sin tareas completadas

  @US24_E03
  Escenario: Solo cuenta el periodo consultado
    Dado que "Ana" completó 1 limpieza hace 40 días
    Y que "Rosa" completó 1 limpieza
    Cuando consulto el reporte de productividad
    Entonces el reporte muestra que "Rosa" completó 1 tarea
    Y "Ana" no aparece en el reporte
