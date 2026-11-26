# language: es
@Sprint3 @US20
Característica: US-20 — Comunicación directa con el hotel
  Como huésped
  quiero comunicarme directamente con el hotel desde la app
  para resolver mis consultas sin llamar.
  Criterios de aceptación del informe (TF1, corte 25/11/2026), ejecutados con Reqnroll contra los casos de uso,
  repositorios y manejadores de eventos reales del servicio Accommodations (base de datos en memoria).

  Antecedentes:
    Dado un hotel con la habitación 305
    Y "Rocío" trabaja en recepción
    Y el huésped "Luis" se aloja en la habitación 305

  @US20_E01
  Escenario: Chat con recepción
    Dado que tengo una consulta
    Cuando envío un mensaje desde el chat
    Entonces la recepción lo recibe y puede responderme en la misma conversación

  @US20_E02
  Escenario: Notificación de respuesta
    Dado que recepción responde mi mensaje
    Cuando esto ocurre
    Entonces recibo una notificación en mi dispositivo
