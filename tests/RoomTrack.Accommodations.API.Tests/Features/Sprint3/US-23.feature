# language: es
@Sprint3 @US23
Característica: US-23 — Alertas automáticas por demoras
  Como administrador
  quiero recibir alertas cuando una tarea o incidencia supera el tiempo estimado de atención
  para intervenir antes de que afecte la experiencia del huésped.
  Criterios de aceptación del informe (TF1, corte 25/11/2026), ejecutados con Reqnroll contra los casos de uso,
  repositorios y manejadores de eventos reales del servicio Accommodations (base de datos en memoria).
  "Checkout inminente" es la salida del huésped anterior: la habitación debe estar limpia antes de la llegada del
  siguiente (hora de check-in del hotel, Bookings__CheckInTime).

  Antecedentes:
    Dado un hotel con la habitación 204
    Y "Mario" trabaja en mantenimiento
    Y "Elena" es administradora del hotel

  @US23_E01
  Escenario: Alerta por checkout próximo
    Dado que una habitación con checkout inminente no está limpia
    Cuando se acerca la hora
    Entonces recibo una alerta

  @US23_E02
  Escenario: Alerta por incidencia crítica
    Dado que una incidencia urgente lleva más tiempo del definido sin resolverse
    Cuando se cumple el plazo
    Entonces se genera una alerta automática

  @US23_E03
  Escenario: Historial de alertas
    Dado que quiero revisar alertas pasadas
    Cuando accedo al registro
    Entonces veo todas las alertas generadas con su resolución
