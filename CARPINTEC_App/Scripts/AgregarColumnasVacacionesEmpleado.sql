-- =========================================================================
-- Script: Agregar columnas de Vacaciones a la tabla Empleado
-- Base de Datos: CARPINTEC (SQL Server)
-- =========================================================================

USE CARPINTEC;
GO

-- 1. Columna FechaInicioVacaciones
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID('Empleado') AND name = 'FechaInicioVacaciones'
)
BEGIN
    ALTER TABLE Empleado ADD FechaInicioVacaciones DATE NULL;
    PRINT 'Columna FechaInicioVacaciones agregada exitosamente.';
END
ELSE
BEGIN
    PRINT 'La columna FechaInicioVacaciones ya existe.';
END
GO

-- 2. Columna FechaFinVacaciones
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID('Empleado') AND name = 'FechaFinVacaciones'
)
BEGIN
    ALTER TABLE Empleado ADD FechaFinVacaciones DATE NULL;
    PRINT 'Columna FechaFinVacaciones agregada exitosamente.';
END
ELSE
BEGIN
    PRINT 'La columna FechaFinVacaciones ya existe.';
END
GO

-- 3. Columna FechaInicioEstado
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID('Empleado') AND name = 'FechaInicioEstado'
)
BEGIN
    ALTER TABLE Empleado ADD FechaInicioEstado DATE NULL;
    PRINT 'Columna FechaInicioEstado agregada exitosamente.';
END
ELSE
BEGIN
    PRINT 'La columna FechaInicioEstado ya existe.';
END
GO

-- 4. Columna FechaFinEstado
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID('Empleado') AND name = 'FechaFinEstado'
)
BEGIN
    ALTER TABLE Empleado ADD FechaFinEstado DATE NULL;
    PRINT 'Columna FechaFinEstado agregada exitosamente.';
END
ELSE
BEGIN
    PRINT 'La columna FechaFinEstado ya existe.';
END
GO

