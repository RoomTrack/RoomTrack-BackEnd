# Sprint 4 — pruebas de aceptación (BDD)

Alcance: US-21 (evaluación de la estadía) y US-26 (satisfacción del huésped por área). Los `.feature` se ejecutan con
**Reqnroll** como tests de xUnit (`../Steps/StayReviewSteps.cs`) contra los casos de uso reales
(`StayReviewCommandService`, `StayReviewQueryService`) y sus repositorios EF Core, sobre una base de datos en memoria.

```sh
dotnet test tests/RoomTrack.Bookings.API.Tests --filter "Category=Sprint4"
```

La evaluación se guarda en la tabla `stay_reviews` (migración `AddStayReviews`): una por reserva, solo del huésped de
la reserva, después del check-out y hasta 30 días después. Calificaciones de 1 a 5: general (obligatoria) y de
recepción, limpieza y mantenimiento (opcionales), más un comentario de hasta 1000 caracteres.

## Trazabilidad

| US / escenario | Estado | Evidencia |
| --- | --- | --- |
| US-21 / E01 | **Ejecutable** | `POST /bookings/{id}/review` guarda las calificaciones; `GET /bookings/{id}/review` las devuelve. |
| US-21 / E02 | **Ejecutable** | El comentario se guarda y el administrador lo lee en `GET /stay-reviews`. |
| US-21 / E03 | **Ejecutable** | Antes del check-out → 409 `stay_review.stay_not_finished`. |
| US-21 / E04 | **Ejecutable** | Una segunda evaluación → 409 `stay_review.already_submitted` (se conserva la primera). |
| US-26 / E01 | **Ejecutable** | `GET /stay-reviews/satisfaction`: cantidad, promedio y % de satisfechos (4 o 5) general y por área. |
| US-26 / E02 | **Ejecutable** | Solo cuentan las evaluaciones enviadas dentro de `from`–`to` (por defecto, los últimos 30 días). |
| US-26 / E03 | **Ejecutable** | Sin evaluaciones: las tres áreas aparecen con cantidad 0 y promedio `null`. |
