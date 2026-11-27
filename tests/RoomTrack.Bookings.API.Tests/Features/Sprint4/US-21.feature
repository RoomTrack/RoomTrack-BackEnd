# language: es
@Sprint4 @US21
Característica: US-21 — Evaluación de la estadía
  Como huésped
  quiero calificar mi estadía y dejar comentarios
  para que el hotel conozca mi experiencia y mejore su servicio.
  Ejecutados con Reqnroll contra los casos de uso y repositorios reales del contexto Bookings (base de datos en
  memoria): la evaluación queda guardada en el servidor junto a la reserva.

  @US21_E01
  Escenario: Calificación de la estadía
    Dado que terminé mi estadía con el check-out
    Cuando califico mi estadía con 4 estrellas y la limpieza con 5
    Entonces mi evaluación queda guardada con 4 estrellas en general y 5 en limpieza

  @US21_E02
  Escenario: Comentario sobre la estadía
    Dado que terminé mi estadía con el check-out
    Cuando califico mi estadía con 3 estrellas y el comentario "La ducha tardaba en calentar"
    Entonces el administrador del hotel lee mi comentario "La ducha tardaba en calentar"

  @US21_E03
  Escenario: Solo se evalúa una estadía terminada
    Dado que todavía estoy alojado en el hotel
    Cuando intento calificar mi estadía
    Entonces el sistema me indica que podré evaluarla después del check-out

  @US21_E04
  Escenario: Una sola evaluación por estadía
    Dado que terminé mi estadía con el check-out
    Y ya califiqué mi estadía
    Cuando intento calificar mi estadía
    Entonces el sistema me indica que ya la evalué
