-- ==============================================================================
-- CARPINTEC - ACTUALIZACIÓN DE RESTRICCIÓN CHECK (CK_Pedido_Estado)
-- ==============================================================================
-- Este script permite verificar y actualizar la restricción CK_Pedido_Estado
-- en la tabla 'Pedido' para que admita todos los estados del taller y cotizaciones.
-- ==============================================================================

USE [CARPINTEC];
GO

PRINT '>>> CONSULTANDO DEFINICIÓN ACTUAL DE CK_Pedido_Estado <<<';
SELECT 
    name AS NombreRestriccion,
    definition AS DefinicionActual
FROM sys.check_constraints 
WHERE name = 'CK_Pedido_Estado';
GO

-- Actualizar restricción para soportar todos los estados válidos:
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Pedido_Estado')
BEGIN
    ALTER TABLE [dbo].[Pedido] DROP CONSTRAINT [CK_Pedido_Estado];
    PRINT '>> Restricción previa CK_Pedido_Estado eliminada.';
END;
GO

ALTER TABLE [dbo].[Pedido] ADD CONSTRAINT [CK_Pedido_Estado] 
CHECK (Estado IN (
    'Pendiente',
    'En Preparación',
    'En Diseño',
    'Diseño',
    'Corte',
    'Ensamble',
    'Pintura',
    'En Producción',
    'Pendiente Entrega',
    'Entregado',
    'Completado',
    'Cancelado'
));
GO

PRINT '>> Restricción CK_Pedido_Estado actualizada exitosamente con los estados completos.';
GO
