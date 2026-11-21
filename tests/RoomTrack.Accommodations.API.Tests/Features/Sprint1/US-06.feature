# language: es
@Sprint1 @US06
Característica: US-06 — Seguimiento de tareas de limpieza
  Como personal de housekeeping
  quiero registrar el avance de mis tareas de limpieza
  para que recepción sepa en todo momento qué habitaciones están listas.

  Antecedentes:
    Dado una tarea de limpieza pendiente para la habitación 101
    Y que la tarea está asignada a "Lucía"

  @US06_E01
  Escenario: Inicio y finalización de la limpieza
    Cuando "Lucía" inicia la tarea
    Y "Lucía" finaliza la tarea
    Entonces la tarea queda "completada"
    Y la tarea registra que la completó "Lucía"

  @US06_E02
  Escenario: Solo la persona asignada inicia la tarea
    Cuando "Mario" intenta iniciar la tarea
    Entonces el sistema rechaza la operación
    Y la tarea queda "pendiente"

  @US06_E03
  Escenario: No se finaliza una tarea que no empezó
    Cuando "Lucía" intenta finalizar la tarea
    Entonces el sistema rechaza la operación
    Y la tarea queda "pendiente"

  @US06_E04
  Escenario: Reporte de un impedimento durante la limpieza
    Dado que "Lucía" inició la tarea
    Cuando "Lucía" reporta el impedimento "Espejo roto" con la incidencia 42
    Entonces la tarea queda "en curso"
    Y la tarea registra la incidencia 42

  @US06_E05
  Escenario: Una tarea cancelada no admite más cambios
    Cuando recepción cancela la tarea por "Habitación equivocada"
    Entonces la tarea queda "cancelada"
    Y "Lucía" ya no puede iniciar la tarea
