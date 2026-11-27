# language: es
@Sprint4 @US25
Característica: US-25 — Indicadores de tiempos de atención
  Como administrador del hotel
  quiero conocer cuánto se demora en promedio limpiar una habitación o resolver una incidencia
  para detectar demoras y mejorar la atención.
  Ejecutados con Reqnroll contra los casos de uso, repositorios y consultas reales del servicio Accommodations
  (base de datos en memoria): los tiempos salen de cuándo se creó, se inició y se terminó cada tarea o incidencia.

  Antecedentes:
    Dado un hotel con la habitación 101 y la habitación 102
    Y "Ana" trabaja en limpieza
    Y "Jorge" trabaja en mantenimiento

  @US25_E01
  Escenario: Tiempo promedio de limpieza
    Dado que "Ana" limpió una habitación en 30 minutos después de esperar 10 minutos
    Y que "Ana" limpió una habitación en 50 minutos después de esperar 20 minutos
    Cuando consulto los indicadores de tiempos de atención
    Entonces limpiar una habitación demora en promedio 40 minutos
    Y una limpieza espera en promedio 15 minutos hasta iniciarse
    Y una limpieza tarda en promedio 55 minutos en total

  @US25_E02
  Escenario: Tiempo promedio de resolución de incidencias
    Dado que "Jorge" resolvió una incidencia en 60 minutos después de esperar 30 minutos
    Y que "Jorge" resolvió una incidencia en 120 minutos después de esperar 30 minutos
    Cuando consulto los indicadores de tiempos de atención
    Entonces resolver una incidencia demora en promedio 90 minutos
    Y una incidencia tarda en promedio 120 minutos en total

  @US25_E03
  Escenario: Sin actividad en el periodo
    Cuando consulto los indicadores de tiempos de atención
    Entonces no hay limpiezas ni incidencias medidas ni promedios
