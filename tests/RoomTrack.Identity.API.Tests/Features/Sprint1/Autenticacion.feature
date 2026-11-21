# language: es
@Sprint1 @IAM
Característica: Autenticación y seguridad de las cuentas
  Como usuario de RoomTrack
  quiero que mi cuenta esté protegida
  para que nadie más pueda operar el hotel o ver mis datos con ella.

  @IAM_E01
  Escenario: Bloqueo temporal tras intentos fallidos
    Dado una cuenta de huésped activa
    Cuando se ingresa una contraseña incorrecta 3 veces seguidas
    Entonces la cuenta queda bloqueada durante 15 minutos
    Y el propietario recibe el aviso del bloqueo

  @IAM_E02
  Escenario: Acceso cuando el bloqueo termina
    Dado una cuenta de huésped activa
    Y que la cuenta fue bloqueada por intentos fallidos
    Cuando el usuario inicia sesión con la contraseña correcta después de 15 minutos
    Entonces el inicio de sesión es aceptado

  @IAM_E03
  Escenario: El personal del hotel debe activar el segundo factor
    Dado una cuenta de "recepción" activa
    Entonces la cuenta debe activar el segundo factor antes de operar
    Cuando el usuario confirma el segundo factor con un código de su aplicación autenticadora
    Entonces la cuenta tiene el segundo factor activo

  @IAM_E04
  Escenario: Un código del segundo factor no se puede reutilizar
    Dado una cuenta de "recepción" con el segundo factor activo
    Y que el usuario ya usó el código actual de su aplicación
    Cuando el usuario intenta usar el mismo código otra vez
    Entonces el código es rechazado por reutilizado

  @IAM_E05
  Esquema del escenario: Contraseñas débiles o comprometidas
    Dado una cuenta de "<rol>" con el correo "carlos.perez@hotel.pe"
    Cuando el usuario elige la contraseña "<contraseña>"
    Entonces la contraseña es rechazada con el código "<código>"

    Ejemplos:
      | rol       | contraseña       | código                  |
      | huésped   | lunes soleado    | password.too_short      |
      | recepción | password123      | password.too_common     |
      | recepción | abcdefghij       | password.repetitive     |
      | recepción | carlos.perez     | password.contains_email |

  @IAM_E06
  Escenario: Una frase larga es una contraseña válida para un huésped
    Dado una cuenta de "huésped" con el correo "ana@correo.pe"
    Cuando el usuario elige la contraseña "tres tigres comen trigo"
    Entonces la contraseña es aceptada

  @IAM_E07
  Escenario: Un cambio de rol cierra todas las sesiones
    Dado una cuenta de "recepción" activa
    Y que el usuario tiene una sesión abierta
    Cuando el administrador le cambia el rol a "limpieza"
    Entonces la sesión abierta deja de ser válida por "cambio de rol"
