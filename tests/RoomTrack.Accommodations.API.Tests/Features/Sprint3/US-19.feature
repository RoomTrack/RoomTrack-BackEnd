# language: es
@Sprint3 @US19
Característica: US-19 — Solicitud de servicios por el huésped
  Como huésped
  quiero solicitar servicios desde la app durante mi estancia
  para no tener que llamar ni acercarme a recepción.
  Criterios de aceptación del informe (TF1, corte 25/11/2026), ejecutados con Reqnroll contra los casos de uso,
  repositorios y manejadores de eventos reales del servicio Accommodations (base de datos en memoria).

  Antecedentes:
    Dado un hotel con la habitación 305
    Y "Rocío" trabaja en recepción
    Y "Ana" trabaja en limpieza
    Y el huésped "Luis" se aloja en la habitación 305

  @US19_E01
  Escenario: Solicitud desde app o portal
    Dado que necesito algo durante mi estancia
    Cuando completo el formulario de solicitud
    Entonces esta llega automáticamente a recepción

  @US19_E02
  Escenario: Confirmación inmediata
    Dado que envié una solicitud
    Cuando se registra
    Entonces recibo una confirmación con un tiempo estimado de atención

  @US19_E03
  Escenario: Seguimiento del estado
    Dado que mi solicitud está en proceso
    Cuando consulto su estado
    Entonces veo si está pendiente, en proceso o resuelta
