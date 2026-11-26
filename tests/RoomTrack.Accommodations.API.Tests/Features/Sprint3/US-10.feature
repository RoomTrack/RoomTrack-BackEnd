# language: es
@Sprint3 @US10
Característica: US-10 — Historial de incidencias por habitación
  Como administrador
  quiero ver el historial de incidencias de cada habitación
  para detectar problemas recurrentes.
  Criterios de aceptación del informe (TF1, corte 25/11/2026), ejecutados con Reqnroll contra los casos de uso,
  repositorios y manejadores de eventos reales del servicio Accommodations (base de datos en memoria).

  Antecedentes:
    Dado un hotel con la habitación 204
    Y "Mario" trabaja en mantenimiento
    Y "Elena" es administradora del hotel

  @US10_E01
  Escenario: Historial detallado
    Dado que en la habitación 204 se reportaron "Ducha con fuga" y "Foco quemado"
    Y "Mario" resolvió "Ducha con fuga"
    Cuando abro el historial de la habitación 204
    Entonces veo todas las incidencias reportadas con fechas y estados

  @US10_E02
  Escenario: Alerta de recurrencia
    Dado que la habitación 204 acumula 3 incidencias "Aire acondicionado no enfría" este mes
    Cuando se reporta una nueva incidencia "Aire acondicionado no enfría" en la habitación 204
    Entonces el sistema resalta el patrón como alerta
    Y "Elena" recibe la alerta de incidencia recurrente
