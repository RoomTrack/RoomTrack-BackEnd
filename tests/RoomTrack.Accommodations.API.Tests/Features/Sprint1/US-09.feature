# language: es
@Sprint1 @US09
Característica: US-09 — Atención y resolución de incidencias
  Como personal de mantenimiento
  quiero tomar, atender y cerrar las incidencias
  para que las habitaciones vuelvan a estar disponibles cuanto antes.

  Antecedentes:
    Dado la habitación 101 disponible en el hotel
    Y "Mario" es personal de mantenimiento del hotel

  @US09_E01
  Escenario: Mantenimiento atiende y resuelve una incidencia
    Dado que recepción reportó la incidencia "Ducha con fuga" con prioridad "alta"
    Cuando "Mario" inicia la incidencia
    Y "Mario" resuelve la incidencia con la nota "Se cambió el empaque"
    Entonces la incidencia queda "resuelta"
    Y la incidencia registra que la resolvió "Mario"

  @US09_E02
  Escenario: La resolución exige describir lo realizado
    Dado que recepción reportó la incidencia "Ducha con fuga" con prioridad "alta"
    Y que "Mario" inició la incidencia
    Cuando "Mario" intenta resolver la incidencia sin nota
    Entonces el sistema rechaza la operación
    Y la incidencia queda "en curso"

  @US09_E03
  Escenario: Al resolver una incidencia que bloqueaba la habitación, esta pasa a limpieza
    Dado que recepción reportó la incidencia "Cerradura rota" con prioridad "crítica" bloqueando la habitación
    Cuando "Mario" inicia la incidencia
    Y "Mario" resuelve la incidencia con la nota "Cerradura reemplazada"
    Entonces la habitación queda en estado "limpieza"

  @US09_E04
  Escenario: El administrador asigna la incidencia a mantenimiento
    Dado que recepción reportó la incidencia "Ducha con fuga" con prioridad "alta"
    Cuando el administrador asigna la incidencia a "Mario"
    Entonces la incidencia queda asignada a "Mario"

  @US09_E05
  Escenario: Solo se asigna a personal de mantenimiento del hotel
    Dado que recepción reportó la incidencia "Ducha con fuga" con prioridad "alta"
    Cuando el administrador intenta asignar la incidencia a "Lucía"
    Entonces el sistema rechaza la operación
    Y la incidencia no tiene persona asignada
