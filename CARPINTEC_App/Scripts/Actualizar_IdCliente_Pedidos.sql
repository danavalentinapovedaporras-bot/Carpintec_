-- ==============================================================================
-- CARPINTEC - SCRIPT DE ACTUALIZACIÓN Y VINCULACIÓN DE IdCliente EN PEDIDO
-- ==============================================================================
-- Descripción:
-- Este script realiza las siguientes acciones:
-- 1. Sincroniza la tabla 'Cliente' con todos los usuarios registrados en 'Usuario'
--    (si un usuario registrado aún no existe como Cliente, lo inserta).
-- 2. Actualiza la columna 'IdCliente' en la tabla 'Pedido' para que coincida con
--    el IdCliente del usuario registrado según su nombre y/o correo electrónico.
-- 3. Proporciona consultas para verificar el estado antes y después.
-- ==============================================================================

USE [CARPINTEC];
GO

PRINT '>>> INICIANDO PROCESO DE SINCRONIZACIÓN ENTRE USUARIO, CLIENTE Y PEDIDO <<<';
PRINT '';

-- ==============================================================================
-- PASO 1: VERIFICAR USUARIOS REGISTRADOS EN LA TABLA Usuario
-- ==============================================================================
PRINT '1. USUARIOS ACTUALMENTE REGISTRADOS EN LA TABLA Usuario:';
SELECT 
    IdUsuario,
    Nombre,
    Apellido,
    Correo,
    Rol,
    Estado
FROM Usuario
ORDER BY IdUsuario ASC;
GO

-- ==============================================================================
-- PASO 2: INSERTAR EN LA TABLA Cliente A LOS USUARIOS QUE NO ESTÉN CREADOS
-- ==============================================================================
PRINT '2. INSERTANDO EN LA TABLA Cliente A LOS USUARIOS REGISTRADOS FALTANTES...';

INSERT INTO Cliente (
    TipoCliente,
    Nombre,
    Apellido,
    NombreEmpresa,
    Documento,
    Contacto,
    Telefono,
    Correo,
    Direccion,
    Ciudad,
    Estado,
    FechaRegistro
)
SELECT 
    'Natural' AS TipoCliente,
    u.Nombre,
    u.Apellido,
    NULL AS NombreEmpresa,
    NULL AS Documento,
    LTRIM(RTRIM(CONCAT(u.Nombre, ' ', ISNULL(u.Apellido, '')))) AS Contacto,
    '' AS Telefono,
    ISNULL(u.Correo, '') AS Correo,
    'Dirección por registrar' AS Direccion,
    'Bogotá' AS Ciudad,
    ISNULL(u.Estado, 'Activo') AS Estado,
    GETDATE() AS FechaRegistro
FROM Usuario u
WHERE (u.Rol = 'Cliente' OR u.Rol IS NULL OR u.Rol = 'Usuario')
  AND NOT EXISTS (
      -- Evitar duplicados por correo
      SELECT 1 FROM Cliente c 
      WHERE (u.Correo IS NOT NULL AND u.Correo <> '' AND LOWER(c.Correo) = LOWER(u.Correo))
         OR (LOWER(c.Nombre) = LOWER(u.Nombre) AND LOWER(ISNULL(c.Apellido, '')) = LOWER(ISNULL(u.Apellido, '')))
  );

PRINT '>> Clientes sincronizados exitosamente desde la tabla Usuario.';
GO

-- ==============================================================================
-- PASO 3: REVISAR ESTADO ACTUAL DE PEDIDOS ANTES DE LA ACTUALIZACIÓN
-- ==============================================================================
PRINT '3. ESTADO DE PEDIDOS ANTES DE LA ACTUALIZACIÓN:';
SELECT 
    p.IdPedido,
    p.CodigoPedido,
    p.Producto,
    p.IdCliente AS IdClienteActual,
    ISNULL(c.Nombre, 'DESCONOCIDO') AS NombreClienteActual,
    ISNULL(c.Apellido, '') AS ApellidoClienteActual,
    p.Estado,
    p.ValorTotal
FROM Pedido p
LEFT JOIN Cliente c ON p.IdCliente = c.IdCliente
ORDER BY p.IdPedido ASC;
GO

-- ==============================================================================
-- PASO 4: ACTUALIZAR IdCliente EN Pedido SEGÚN LOS NOMBRES/CORREOS DE COTIZACIÓN
-- ==============================================================================
PRINT '4. ACTUALIZANDO IdCliente EN Pedido A PARTIR DE COTIZACIONES VINCULADAS...';

-- 4.1 Coincidencia por correo en Cotizacion -> Cliente
UPDATE p
SET p.IdCliente = c.IdCliente
FROM Pedido p
INNER JOIN Cotizacion cot ON p.IdCotizacion = cot.IdCotizacion
INNER JOIN Cliente c ON LOWER(cot.CorreoCliente) = LOWER(c.Correo)
WHERE cot.CorreoCliente IS NOT NULL 
  AND cot.CorreoCliente <> ''
  AND p.IdCliente <> c.IdCliente;

-- 4.2 Coincidencia por nombre completo en Cotizacion -> Cliente
UPDATE p
SET p.IdCliente = c.IdCliente
FROM Pedido p
INNER JOIN Cotizacion cot ON p.IdCotizacion = cot.IdCotizacion
INNER JOIN Cliente c ON 
    LOWER(cot.NombreCliente) = LOWER(LTRIM(RTRIM(CONCAT(c.Nombre, ' ', ISNULL(c.Apellido, '')))))
    OR LOWER(cot.NombreCliente) = LOWER(c.Nombre)
WHERE cot.NombreCliente IS NOT NULL 
  AND cot.NombreCliente <> ''
  AND p.IdCliente <> c.IdCliente;

GO

-- ==============================================================================
-- PASO 4.5: CREAR PEDIDOS PARA TODAS LAS COTIZACIONES QUE AÚN NO TENGAN PEDIDO
-- ==============================================================================
PRINT '4.5. GENERANDO PEDIDOS PARA COTIZACIONES DEL CLIENTE QUE NO TENGAN PEDIDO...';

INSERT INTO Pedido (
    CodigoPedido,
    Producto,
    IdCotizacion,
    IdCliente,
    FechaSolicitud,
    FechaEntrega,
    Estado,
    ValorTotal,
    Observaciones,
    FechaRegistro
)
SELECT 
    REPLACE(cot.Folio, 'COT', 'PED') AS CodigoPedido,
    ISNULL(cot.DetalleProducto, 'Mueble a Medida') AS Producto,
    cot.IdCotizacion,
    cot.IdCliente,
    ISNULL(cot.Fecha, CAST(GETDATE() AS DATE)) AS FechaSolicitud,
    DATEADD(DAY, 15, ISNULL(cot.Fecha, CAST(GETDATE() AS DATE))) AS FechaEntrega,
    CASE 
        WHEN cot.Estado IN ('Finalizada', 'Entregado') THEN 'Completado'
        WHEN cot.Estado = 'Aprobada' THEN 'En Diseño'
        ELSE 'Pendiente'
    END AS Estado,
    ISNULL(cot.Total, 320000) AS ValorTotal,
    CONCAT('Pedido generado desde cotización ', cot.Folio, '. Madera: ', ISNULL(cot.TipoMadera, 'Pino'), ', Medidas: ', ISNULL(cot.Medidas, 'A medida')) AS Observaciones,
    ISNULL(cot.FechaRegistro, GETDATE()) AS FechaRegistro
FROM Cotizacion cot
WHERE NOT EXISTS (
    SELECT 1 FROM Pedido p WHERE p.IdCotizacion = cot.IdCotizacion
);

PRINT '>> Pedidos creados a partir de cotizaciones pendientes.';
GO

-- ==============================================================================
-- PASO 5: SI HAY PEDIDOS CON IdCliente HUÉRFANO O DEFAULT (Ej: 1) Y SE DESEA
-- ASIGNAR AL PRIMER CLIENTE REGISTRADO O A UN CLIENTE ESPECÍFICO:
-- ==============================================================================
PRINT '5. VINCULANDO PEDIDOS SIN CLIENTE VÁLIDO AL CLIENTE REGISTRADO PRINCIPAL...';

-- Obtenemos el IdCliente correspondiente al primer usuario registrado con rol Cliente
DECLARE @IdClientePrincipal INT;

SELECT TOP 1 @IdClientePrincipal = c.IdCliente
FROM Cliente c
INNER JOIN Usuario u ON LOWER(c.Correo) = LOWER(u.Correo) OR LOWER(c.Nombre) = LOWER(u.Nombre)
WHERE u.Rol = 'Cliente'
ORDER BY c.IdCliente ASC;

IF @IdClientePrincipal IS NOT NULL
BEGIN
    -- Actualizamos pedidos que no tengan relación válida o que apunten a un ID inexistente
    UPDATE Pedido
    SET IdCliente = @IdClientePrincipal
    WHERE IdCliente NOT IN (SELECT IdCliente FROM Cliente);

    PRINT '>> Pedidos verificados y vinculados al Cliente ID: ' + CAST(@IdClientePrincipal AS VARCHAR(10));
END
GO

-- ==============================================================================
-- PASO 6: CONSULTA FINAL DE VERIFICACIÓN
-- ==============================================================================
PRINT '6. RESULTADO FINAL: PEDIDOS VINCULADOS A USUARIOS Y CLIENTES REGISTRADOS:';

SELECT 
    p.IdPedido,
    p.CodigoPedido,
    p.Producto,
    p.IdCliente,
    LTRIM(RTRIM(CONCAT(c.Nombre, ' ', ISNULL(c.Apellido, '')))) AS ClienteNombreCompleto,
    c.Correo AS CorreoCliente,
    u.IdUsuario,
    u.Rol AS RolUsuario,
    p.Estado AS EstadoPedido,
    p.ValorTotal,
    p.FechaSolicitud,
    p.FechaEntrega
FROM Pedido p
INNER JOIN Cliente c ON p.IdCliente = c.IdCliente
LEFT JOIN Usuario u ON LOWER(c.Correo) = LOWER(u.Correo) OR LOWER(c.Nombre) = LOWER(u.Nombre)
ORDER BY p.IdPedido ASC;
GO

PRINT '>>> SINCRONIZACIÓN DE IdCliente EN TABLA Pedido COMPLETADA CON ÉXITO <<<';
GO
