# language: es
@Sprint1 @US05
Característica: US-05 — Asignación de tareas de limpieza
  Como recepcionista
  quiero asignar las tareas de limpieza al personal de housekeeping
  para que cada habitación pendiente tenga un responsable.

  Antecedentes:
    Dado una tarea de limpieza pendiente para la habitación 101

  @US05_E01
  Escenario: Recepción asigna la tarea a una persona de limpieza
    Cuando recepción asigna la tarea a "Lucía"
    Entonces la tarea queda asignada a "Lucía"
    Y el historial de la tarea registra la "asignación"

  @US05_E02
  Escenario: Recepción reasigna la tarea a otra persona
    Dado que la tarea está asignada a "Lucía"
    Cuando recepción asigna la tarea a "Mario"
    Entonces la tarea queda asignada a "Mario"
    Y el historial de la tarea registra la "reasignación"

  @US05_E03
  Escenario: El personal de limpieza toma una tarea libre
    Cuando "Lucía" asigna la tarea a "Lucía"
    Entonces la tarea queda asignada a "Lucía"

  @US05_E04
  Escenario: El personal de limpieza no puede asignar tareas a otra persona
    Cuando "Lucía" intenta asignar la tarea a "Mario"
    Entonces el sistema rechaza la operación
    Y la tarea sigue sin persona asignada

  @US05_E05
  Escenario: Recepción cambia la prioridad de la tarea
    Cuando recepción cambia la prioridad de la tarea a "urgente"
    Entonces la tarea tiene prioridad "urgente"
