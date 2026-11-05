namespace Brar.Application.Natural;

public sealed record EmpresaDto(
    int Id, string? Nombre, string? Nit, string? Direccion, string? Telefono,
    bool Activa, int TotalPersonas, int TotalAccesos, int AccesosActivos);

public sealed record EmpresaSolicitud(string? Nombre, string? Nit, string? Direccion, string? Telefono);

public sealed record AdminEmpresaSolicitud(
    string? Nombre, string? Apellido, string? Cedula, string? Email, string? Celular,
    string? NombreUsuario, string? Contrasena);

public sealed record CrearEmpresaSolicitud(
    string? Nombre, string? Nit, string? Direccion, string? Telefono, AdminEmpresaSolicitud? Admin);

public sealed record AccesoDto(
    int PersonaId, string Nombre, string Apellido, string? Email, int? UsuarioId, string? NombreUsuario,
    bool TieneAcceso, bool Activo, string? Rol);

public sealed record CrearAccesoSolicitud(int PersonaId, string? NombreUsuario, string? Contrasena, string? Rol);

public sealed record CambiarRolSolicitud(string? Rol);

public sealed record RestablecerContrasenaSolicitud(string? NuevaContrasena);

public sealed record ImpersonarSolicitud(string? Actor, int Minutos);

public sealed record PersonaSesionDto(int Id, int IdUsuario, string Nombre, int EmpresaId, string? NombreEmpresa, string Rol);

public sealed record ImpersonacionDto(string Token, DateTime ExpiraEn, PersonaSesionDto Persona);
