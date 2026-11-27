# language: es
@Sprint4 @US34
Característica: US-34 — Exportación y envío automático de reportes
  Como administrador del hotel
  quiero descargar los reportes desde el servidor y programar su envío automático por correo
  para compartirlos con la gerencia sin tener que armarlos a mano.
  Ejecutados con Reqnroll contra los casos de uso, repositorios y el controlador reales del servicio Accommodations
  (base de datos en memoria); los correos son los que el servicio entrega al outbox.

  Antecedentes:
    Dado un hotel con la habitación 101 y la habitación 102
    Y "Ana" trabaja en limpieza
    Y que "Ana" completó 2 limpiezas

  @US34_E01
  Escenario: Descarga del reporte
    Cuando descargo el reporte de productividad en CSV
    Entonces el archivo tiene una fila de "Ana" con 2 limpiezas

  @US34_E02
  Escenario: Envío automático programado
    Dado que programé el envío semanal de los reportes a "gerencia@hotel.pe"
    Cuando llega la hora del siguiente envío y se ejecuta el programador
    Entonces "gerencia@hotel.pe" recibe el reporte semanal con el trabajo de "Ana"
    Y el siguiente envío queda para una semana después

  @US34_E03
  Escenario: Nada se envía antes de tiempo
    Dado que programé el envío semanal de los reportes a "gerencia@hotel.pe"
    Cuando se ejecuta el programador antes de la hora del envío
    Entonces no se envía ningún reporte
