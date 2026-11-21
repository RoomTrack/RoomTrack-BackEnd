# language: es
@Sprint1 @US08
Característica: US-08 — Reporte de incidencias de mantenimiento
  Como personal del hotel
  quiero reportar las incidencias de una habitación
  para que mantenimiento las atienda y la habitación no se ofrezca mientras no esté en condiciones.

  Antecedentes:
    Dado la habitación 101 disponible en el hotel

  @US08_E01
  Escenario: Recepción reporta una incidencia
    Cuando recepción reporta la incidencia "Ducha con fuga" con prioridad "alta"
    Entonces la incidencia queda "pendiente"
    Y la incidencia no tiene persona asignada
    Y la habitación queda en estado "disponible"

  @US08_E02
  Escenario: Una incidencia deja la habitación fuera de servicio
    Cuando recepción reporta la incidencia "Cerradura rota" con prioridad "crítica" bloqueando la habitación
    Entonces la habitación queda en estado "mantenimiento"
    Y la habitación no puede volver a "disponible" mientras la incidencia siga abierta

  @US08_E03
  Escenario: El título de la incidencia es obligatorio
    Cuando recepción intenta reportar la incidencia "ab" con prioridad "baja"
    Entonces el sistema rechaza la operación

  @US08_E04
  Escenario: Escalamiento de una incidencia crítica no atendida
    Dado que recepción reportó la incidencia "Fuga de gas" con prioridad "crítica"
    Cuando pasan 61 minutos sin que nadie inicie la incidencia
    Entonces la incidencia se escala a los administradores

  @US08_E05
  Escenario: Una incidencia que no es crítica no se escala
    Dado que recepción reportó la incidencia "Foco quemado" con prioridad "media"
    Cuando pasan 61 minutos sin que nadie inicie la incidencia
    Entonces la incidencia no se escala
