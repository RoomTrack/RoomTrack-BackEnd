# language: es
@Sprint4 @US18
Característica: US-18 — Panel centralizado del administrador
  Como administrador del hotel
  quiero ver en un solo panel el estado de la operación de hoy
  para detectar a tiempo lo que requiere mi atención sin revisar cada pantalla.
  Ejecutados con Reqnroll contra los casos de uso, repositorios y consultas reales del servicio Accommodations
  (base de datos en memoria); la recepción de hoy la informa el servicio Bookings por su API interna.

  Antecedentes:
    Dado un hotel con la habitación 101 y la habitación 102
    Y "Ana" trabaja en limpieza
    Y "Jorge" trabaja en mantenimiento

  @US18_E01
  Escenario: Operación de hoy en una sola consulta
    Dado que la habitación 101 está ocupada
    Y que "Ana" está limpiando la habitación 102
    Y que recepción reportó una incidencia crítica en la habitación 101
    Cuando consulto el panel del administrador
    Entonces el panel muestra 50 % de ocupación con 1 habitación ocupada y 1 en limpieza
    Y el panel muestra 1 tarea de limpieza abierta
    Y el panel muestra 1 incidencia abierta y 1 crítica
    Y la incidencia crítica de la habitación 101 encabeza lo que requiere atención

  @US18_E02
  Escenario: Tareas atrasadas
    Dado que "Ana" está limpiando la habitación 102
    Cuando pasan 61 minutos y consulto el panel del administrador
    Entonces el panel muestra 1 tarea de limpieza atrasada

  @US18_E03
  Escenario: Recepción y evaluaciones del día
    Dado que hoy hay 3 llegadas, 5 huéspedes alojados y 2 salidas con promedio de evaluaciones 4.5
    Cuando consulto el panel del administrador
    Entonces el panel muestra 3 llegadas, 5 huéspedes alojados y 2 salidas
    Y el panel muestra el promedio de evaluaciones 4.5

  @US18_E04
  Escenario: El panel responde aunque Bookings no esté disponible
    Dado que la habitación 101 está ocupada
    Y que el servicio de reservas no responde
    Cuando consulto el panel del administrador
    Entonces el panel muestra 50 % de ocupación con 1 habitación ocupada y 0 en limpieza
    Y el panel indica que la recepción no está disponible
