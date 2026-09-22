-- =========================================================================
-- Script: Sincronización de Usuarios y Empleados (CARPINTEC_App)
-- Base de Datos: CARPINTEC (SQL Server)
-- =========================================================================

USE CARPINTEC;
GO

-- 1. Verificar y agregar la columna IdUsuario en la tabla Empleado si no existe
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID('Empleado') AND name = 'IdUsuario'
)
BEGIN
    ALTER TABLE Empleado ADD IdUsuario INT NULL;
    PRINT 'Columna IdUsuario agregada a la tabla Empleado exitosamente.';
END
ELSE
BEGIN
    PRINT 'La columna IdUsuario ya existe en la tabla Empleado.';
END
GO

-- 2. Verificar y agregar la clave foránea FK_Empleado_Usuario si no existe
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys 
    WHERE name = 'FK_Empleado_Usuario'
)
BEGIN
    ALTER TABLE Empleado 
    ADD CONSTRAINT FK_Empleado_Usuario 
    FOREIGN KEY (IdUsuario) REFERENCES Usuario(IdUsuario);
    PRINT 'Clave foránea FK_Empleado_Usuario creada exitosamente.';
END
ELSE
BEGIN
    PRINT 'La clave foránea FK_Empleado_Usuario ya existe.';
END
GO

-- 3. Consulta de verificación: Ver empleados y sus usuarios vinculados
SELECT 
    e.IdEmpleado,
    e.Documento,
    e.Nombre,
    e.Apellido,
    e.Cargo,
    e.Correo,
    e.Estado AS EstadoEmpleado,
    u.IdUsuario,
    u.Rol AS RolUsuario,
    u.Estado AS EstadoUsuario
FROM Empleado e
LEFT JOIN Usuario u ON e.IdUsuario = u.IdUsuario;
GO
