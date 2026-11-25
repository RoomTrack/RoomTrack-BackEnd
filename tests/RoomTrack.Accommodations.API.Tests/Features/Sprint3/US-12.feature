# language: es
@Sprint3 @US12
Característica: US-12 — Comunicación entre áreas operativas
  Como miembro del personal operativo
  quiero comunicarme con otras áreas sobre las tareas
  para coordinar sin llamadas ni mensajes informales.
  Criterios de aceptación del informe (TF1, corte 25/11/2026), ejecutados con Reqnroll contra los casos de uso,
  repositorios y manejadores de eventos reales del servicio Accommodations (base de datos en memoria).

  Antecedentes:
    Dado un hotel con la habitación 101
    Y "Rocío" trabaja en recepción
    Y "Ana" trabaja en limpieza
    Y "Rosa" trabaja en limpieza
    Y "Carla" trabaja en limpieza

  @US12_E01
  Escenario: Mensajería por tarea
    Dado que tengo una tarea asignada
    Cuando necesito aclarar un detalle
    Entonces puedo comentar directamente sobre esa tarea y notificar al responsable

  @US12_E02
  Escenario: Canal por área
    Dado que necesito comunicarme con todo el equipo de limpieza
    Cuando envío un mensaje al canal del área
    Entonces todos los miembros lo reciben

  @US12_E03
  Escenario: Historial de comunicación
    Dado que reviso una tarea cerrada
    Cuando abro el historial de la tarea
    Entonces veo todos los comentarios y cambios de estado relacionados
