# language: es
@Sprint3 @US30
Característica: US-30 — Programación de mantenimiento preventivo
  Como personal de mantenimiento
  quiero programar tareas de mantenimiento preventivo recurrentes
  para reducir la aparición de incidencias no planificadas.
  Criterios de aceptación del informe (TF1, corte 25/11/2026), ejecutados con Reqnroll contra los casos de uso,
  repositorios y manejadores de eventos reales del servicio Accommodations (base de datos en memoria).

  Antecedentes:
    Dado un hotel con la habitación 204
    Y "Mario" trabaja en mantenimiento

  @US30_E01
  Escenario: Crear tarea recurrente
    Dado que "Mario" define la tarea preventiva "Limpiar filtros del aire" de la habitación 204 cada 30 días desde el 15/01/2030
    Cuando llega el 15/01/2030
    Entonces el sistema genera la tarea preventiva para mantenimiento
    Y la siguiente se programa para el 14/02/2030

  @US30_E02
  Escenario: Recordatorio de tarea próxima
    Dado que "Mario" define la tarea preventiva "Limpiar filtros del aire" de la habitación 204 cada 30 días desde el 15/01/2030
    Cuando llega el 12/01/2030
    Entonces "Mario" recibe un recordatorio de "Limpiar filtros del aire"
    Y todavía no se genera la tarea

  @US30_E03
  Escenario: Historial de mantenimiento por habitación
    Dado que en la habitación 204 se reportaron "Ducha con fuga" y "Foco quemado"
    Y que "Mario" define la tarea preventiva "Limpiar filtros del aire" de la habitación 204 cada 30 días desde el 15/01/2030
    Y llegó el 15/01/2030
    Cuando abro el historial de la habitación 204
    Entonces veo tanto incidencias correctivas como mantenimientos preventivos realizados
