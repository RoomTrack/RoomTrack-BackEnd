# language: es
@Sprint3 @US22
Característica: US-22 — Notificaciones push al personal (entrega)
  Como miembro del Staff Operativo
  quiero recibir notificaciones push cuando se me asigna o actualiza una tarea
  para responder de inmediato sin revisar constantemente la plataforma.
  Los servicios publican cada notificación en RabbitMQ; el worker de Notifications la entrega a Firebase Cloud
  Messaging. Los escenarios ejecutan el consumidor real contra un proveedor simulado.

  @US22_E01
  Escenario: Nueva tarea asignada
    Dado que se me asigna una tarea y mi celular está registrado
    Cuando esto ocurre
    Entonces recibo una notificación push inmediata

  @US22_E02
  Escenario: Cambio de prioridad
    Dado que una tarea asignada cambia a urgente
    Cuando ocurre el cambio
    Entonces recibo una notificación destacada

  @US22_E03
  Escenario: Un dispositivo desinstalado deja de recibir notificaciones
    Dado una notificación para un celular activo y otro donde se desinstaló la app
    Cuando el worker la entrega
    Entonces se avisa a Identity que olvide el dispositivo desinstalado

  @US22_E04
  Escenario: Proveedor no disponible para algunos dispositivos
    Dado una notificación para dos celulares y el proveedor solo alcanza a uno
    Cuando el worker la entrega
    Entonces se reintenta solo el dispositivo pendiente, sin duplicar la notificación

  @US22_E05
  Escenario: Correo rechazado de forma permanente
    Dado un correo para una dirección que el servidor rechaza para siempre
    Cuando el worker lo procesa
    Entonces no se reintenta
