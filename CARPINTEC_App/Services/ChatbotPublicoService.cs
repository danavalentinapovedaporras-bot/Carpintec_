using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CARPINTEC_App.Services
{
    /// <summary>
    /// Servicio independiente para el Chatbot Público de CARPINTEC.
    /// Atiende exclusivamente a visitantes de la página web principal con información
    /// corporativa general, servicios, productos, preguntas frecuentes y contacto.
    /// No tiene acceso ni interactúa con datos sensibles del ERP ni de la base de datos administrativa.
    /// </summary>
    public class ChatbotPublicoService
    {
        public ChatbotPublicoService()
        {
        }

        /// <summary>
        /// Procesa la consulta del visitante de la página web y responde según las reglas del Chatbot Público.
        /// </summary>
        public async Task<string> ResponderPublicoAsync(string mensaje)
        {
            // Simular respuesta asíncrona no bloqueante
            await Task.Yield();

            if (string.IsNullOrWhiteSpace(mensaje))
            {
                return "👋 ¡Hola! Bienvenido a **CARPINTEC** 🪵. Soy tu asesor virtual en la página web. ¿En qué te podemos colaborar hoy? Puedes consultarme sobre quiénes somos, nuestros servicios de carpintería, datos de contacto o cómo solicitar una cotización.";
            }

            string normalizado = NormalizarTexto(mensaje);

            // =========================================================================
            // REGLA 1: Solicitud de cotizaciones, pedidos, compras, visitas y seguimiento
            // =========================================================================
            // Cada vez que un usuario pregunte sobre cotizar, hacer pedido, registrar proyecto,
            // agendar visita o seguimiento, se le exige crear cuenta e iniciar sesión.
            if (RequiereCuentaOSesion(normalizado))
            {
                return GenerarRespuestaRequiereCuenta();
            }

            // =========================================================================
            // REGLA 2: Bloqueo de consultas administrativas o de ERP interno
            // =========================================================================
            // Protege la privacidad del negocio ante preguntas sobre empleados, base de datos interna, etc.
            if (EsConsultaAdministrativaOERP(normalizado))
            {
                return GenerarRespuestaAccesoRestringido();
            }

            // =========================================================================
            // REGLA 3: Saludos y bienvenida
            // =========================================================================
            if (EsSaludo(normalizado))
            {
                return GenerarSaludoPublico();
            }

            // =========================================================================
            // REGLA 4: Agradecimientos y despedidas
            // =========================================================================
            if (EsAgradecimientoODespedida(normalizado))
            {
                return GenerarAgradecimientoPublico();
            }

            // =========================================================================
            // REGLA 5: Información sobre la empresa (Quiénes somos, misión, visión, historia)
            // =========================================================================
            if (EsConsultaEmpresa(normalizado))
            {
                return GenerarInformacionEmpresa();
            }

            // =========================================================================
            // REGLA 6: Servicios y Productos (Cocinas, closets, muebles a medida, maderas)
            // =========================================================================
            if (EsConsultaServiciosOProductos(normalizado))
            {
                return GenerarServiciosYProductos();
            }

            // =========================================================================
            // REGLA 7: Contacto, Ubicación y Horarios de Atención
            // =========================================================================
            if (EsConsultaContacto(normalizado))
            {
                return GenerarInformacionContacto();
            }

            // =========================================================================
            // REGLA 8: Preguntas Frecuentes (Garantías, tiempos de entrega, materiales, pagos)
            // =========================================================================
            if (EsPreguntasFrecuentes(normalizado))
            {
                return GenerarPreguntasFrecuentes();
            }

            // =========================================================================
            // REGLA 9: Guía de Navegación por la Página Web
            // =========================================================================
            if (EsGuiaNavegacion(normalizado))
            {
                return GenerarGuiaNavegacion();
            }

            // =========================================================================
            // REGLA 10: Respuesta por defecto cuando no se comprende la pregunta
            // =========================================================================
            return GenerarRespuestaPorDefecto(mensaje);
        }

        #region Normalización de Texto

        private static string NormalizarTexto(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

            string descompuesto = texto.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();

            foreach (char c in descompuesto)
            {
                UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(c);
                if (cat != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }

            string limpio = sb.ToString().Normalize(NormalizationForm.FormC).ToLower();
            limpio = Regex.Replace(limpio, @"[¿?¡!.,;:()\-_/]", " ");
            return Regex.Replace(limpio, @"\s+", " ").Trim();
        }

        private static bool ContienePalabra(string texto, params string[] palabras)
        {
            return palabras.Any(p => texto.Contains(p, StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region Clasificadores de Intención para Visitantes Públicos

        /// <summary>
        /// Detecta si el visitante pregunta por cotizaciones, pedidos, proyectos, compras, visitas o seguimiento.
        /// </summary>
        private static bool RequiereCuentaOSesion(string t)
        {
            // Triggers de cotizaciones y presupuestos
            bool cotizacion = ContienePalabra(t,
                "cotizar", "cotizacion", "cotizaciones", "presupuesto", "presupuestos",
                "cuanto cuesta", "cuanto me cobra", "cuanto vale hacer", "solicitar cotizacion",
                "quiero cotizar", "pedir cotizacion", "costo de hacer", "precio para hacer");

            // Triggers de pedidos y encargos
            bool pedidos = ContienePalabra(t,
                "pedido", "pedidos", "hacer un pedido", "realizar pedido", "encargar",
                "encargo", "mandar a hacer", "fabricar mueble", "orden de compra");

            // Triggers de proyectos y compras
            bool proyectosYCompras = ContienePalabra(t,
                "registrar proyecto", "subir plano", "mi proyecto", "comprar un producto",
                "comprar mueble", "comprar", "adquirir", "contratar servicio", "contratar");

            // Triggers de visitas técnicas y seguimiento
            bool visitasYSeguimiento = ContienePalabra(t,
                "agendar visita", "visita tecnica", "visita a domicilio", "medir en mi casa",
                "estado de mi cotizacion", "estado de mi pedido", "seguimiento", "consultar estado",
                "como va mi pedido", "mi orden");

            return cotizacion || pedidos || proyectosYCompras || visitasYSeguimiento;
        }

        /// <summary>
        /// Detecta consultas de usuarios que intentan consultar datos internos del ERP administrativo.
        /// </summary>
        private static bool EsConsultaAdministrativaOERP(string t)
        {
            return ContienePalabra(t,
                "base de datos", "erp", "panel de administracion", "cuantos clientes", "ver clientes",
                "nomina", "salario de", "empleados del sistema", "facturas registradas",
                "total facturado", "stock en bodega", "usuarios del sistema", "bloqueados",
                "usuarios registrados", "ingresos del mes", "ganancias del taller");
        }

        private static bool EsSaludo(string t)
        {
            return ContienePalabra(t,
                "hola", "buenos dias", "buenas tardes", "buenas noches", "que tal",
                "saludos", "buenas", "buen dia", "hey");
        }

        private static bool EsAgradecimientoODespedida(string t)
        {
            return ContienePalabra(t,
                "gracias", "muchas gracias", "mil gracias", "te agradezco", "adios",
                "chao", "hasta luego", "nos vemos", "excelente gracias");
        }

        private static bool EsConsultaEmpresa(string t)
        {
            return ContienePalabra(t,
                "quienes somos", "acerca de", "sobre nosotros", "la empresa", "historia",
                "mision", "vision", "valores", "que es carpintec", "donde nacio");
        }

        private static bool EsConsultaServiciosOProductos(string t)
        {
            return ContienePalabra(t,
                "servicios", "servicio", "productos", "producto", "catalogo", "que hacen",
                "que ofrecen", "que fabrican", "cocinas", "closets", "armarios",
                "muebles", "puertas", "maderas", "materiales que usan", "madera maciza");
        }

        private static bool EsConsultaContacto(string t)
        {
            return ContienePalabra(t,
                "contacto", "contactar", "telefono", "celular", "whatsapp", "correo",
                "email", "direccion", "donde estan", "ubicacion", "donde quedan",
                "horario", "horarios", "a que hora abren", "cuando atienden");
        }

        private static bool EsPreguntasFrecuentes(string t)
        {
            return ContienePalabra(t,
                "preguntas frecuentes", "faq", "garantia", "garantias", "tiempo de entrega",
                "cuanto demoran", "cuanto tardan", "formas de pago", "metodos de pago",
                "envios", "instalacion a domicilio", "hacen envios", "incluye instalacion");
        }

        private static bool EsGuiaNavegacion(string t)
        {
            return ContienePalabra(t,
                "navegar", "pagina", "web", "secciones", "como usar la pagina",
                "donde encuentro", "donde ver", "menu de la pagina", "ayuda de la pagina");
        }

        #endregion

        #region Generadores de Respuestas Institucionales y Públicas

        /// <summary>
        /// Respuesta obligatoria exigida cuando un visitante consulta por cotizaciones, pedidos o servicios de seguimiento.
        /// </summary>
        private static string GenerarRespuestaRequiereCuenta()
        {
            return "📋 **Solicitud de Cotizaciones y Servicios en CARPINTEC**\n\n" +
                   "Para solicitar una cotización o acceder a este servicio, primero debes crear una cuenta e iniciar sesión en CARPINTEC. Una vez registrado, podrás realizar solicitudes, hacer seguimiento a tus cotizaciones y gestionar tus pedidos de forma segura.\n\n" +
                   "👉 **¿Aún no tienes cuenta?** Regístrate gratis en menos de un minuto:\n" +
                   "🔗 **[Crear una cuenta en CARPINTEC](/Registro)**\n\n" +
                   "🔑 **¿Ya estás registrado?** Ingresa con tus credenciales aquí:\n" +
                   "🔗 **[Iniciar sesión](/Login)**\n\n" +
                   "💡 *Si tienes dudas generales sobre quiénes somos, nuestros horarios o servicios, con gusto te oriento.*";
        }

        /// <summary>
        /// Bloqueo educado para mantener desacoplada la información interna/administrativa.
        /// </summary>
        private static string GenerarRespuestaAccesoRestringido()
        {
            return "🔒 **Información de Uso Administrativo**\n\n" +
                   "Esta consulta corresponde a información y funciones del sistema de gestión interno (ERP) de CARPINTEC. Como asesor público de la página web, solo brindo información corporativa para visitantes.\n\n" +
                   "Si eres colaborador o administrador del sistema, por favor ingresa al portal:\n" +
                   "🔗 **[Iniciar sesión en el Portal Administrativo](/Login)**";
        }

        private static string GenerarSaludoPublico()
        {
            return "👋 ¡Hola! Bienvenido a **CARPINTEC** 🪵, especialistas en carpintería y ebanistería arquitectónica.\n\n" +
                   "Soy tu asesor virtual y estoy aquí para brindarte información general sobre nuestra empresa. Puedes consultarme sobre:\n\n" +
                   "• 🏢 *'¿Quiénes somos?'* (nuestra historia, misión y visión)\n" +
                   "• 🪵 *'Servicios y productos'* (cocinas, closets, muebles a medida)\n" +
                   "• 📞 *'Contacto y horarios'* (teléfonos, ubicación y horarios de atención)\n" +
                   "• ❓ *'Preguntas frecuentes'* (garantías, tiempos y métodos de pago)\n" +
                   "• 🧭 *'Navegar en la página'* (guía de secciones del sitio web)\n" +
                   "• 📄 *'Solicitar una cotización'* (requisitos para cotizar)\n\n" +
                   "¿En qué te podemos colaborar hoy?";
        }

        private static string GenerarAgradecimientoPublico()
        {
            return "🪵 ¡Con mucho gusto! Gracias por visitar **CARPINTEC**. Si deseas cotizar un proyecto a medida o consultar tus solicitudes, recuerda registrarte o iniciar sesión en nuestro portal. ¡Que tengas un excelente día!";
        }

        private static string GenerarInformacionEmpresa()
        {
            return "🏢 **Acerca de CARPINTEC — Pasión y Maestría en Madera**\n\n" +
                   "Carpintec es una empresa colombiana dedicada al diseño, fabricación e instalación de soluciones mobiliarias en madera de alta gama. Combinamos la calidez y nobleza de las maderas finas con herrajes de última tecnología para crear espacios acogedores, funcionales y duraderos.\n\n" +
                   "🎯 **Nuestra Misión:**\n" +
                   "Diseñar y fabricar muebles personalizados con altos estándares de calidad, innovación y cumplimiento, transformando las ideas de nuestros clientes en piezas únicas para el hogar y la oficina.\n\n" +
                   "🌟 **Nuestra Visión:**\n" +
                   "Ser referentes en carpintería y diseño de interiores en madera a nivel regional y nacional, reconocidos por la excelencia artesanal, el cumplimiento en entregas y la satisfacción de nuestros clientes.\n\n" +
                   "💎 **Nuestros Pilares:**\n" +
                   "• Acabados meticulosos y maderas certificadas.\n" +
                   "• Asesoría en diseño según tu espacio.\n" +
                   "• Cumplimiento riguroso en fechas de entrega.";
        }

        private static string GenerarServiciosYProductos()
        {
            return "🪵 **Servicios y Productos que Ofrecemos en CARPINTEC**\n\n" +
                   "Diseñamos y fabricamos muebles a la medida que se adaptan con precisión a tus necesidades y espacios:\n\n" +
                   "🍳 **1. Cocinas Integrales Personalizadas:**\n" +
                   "  Módulos modernos o clásicos, alacenas ergonómicas, barras tipo isla, mesones en granito o cuarzo y cajoneras con cierre suave.\n\n" +
                   "🚪 **2. Closets, Armarios y Walk-in Closets:**\n" +
                   "  Aprovechamiento total del espacio con puertas corredizas o batientes, zapateros extraíbles, pantaloneros y divisiones interiores optimizadas.\n\n" +
                   "🛋️ **3. Muebles a Medida para Hogar y Oficina:**\n" +
                   "  Centros de entretenimiento para televisión, escritorios ergonómicos de trabajo, bibliotecas, mesas de comedor y estanterías.\n\n" +
                   "🚪 **4. Puertas y Carpintería Arquitectónica:**\n" +
                   "  Puertas de entrada principal macizas, puertas de paso, revestimientos de pared y molduras.\n\n" +
                   "🌳 **Maderas y Acabados:**\n" +
                   "  Trabajamos Roble, Cedro, Flor Morado, Pino canadiense y tableros melamínicos de alta densidad (MDF/RH) resistentes a la humedad.\n\n" +
                   "💡 *Recuerda que para cotizar cualquiera de estos diseños debes contar con una cuenta activa en el sistema.*";
        }

        private static string GenerarInformacionContacto()
        {
            return "📞 **Canales de Atención y Contacto de CARPINTEC**\n\n" +
                   "Estamos a tu disposición para atender tus consultas y asesorarte en tu próximo proyecto:\n\n" +
                   "📍 **Ubicación Taller y Showroom:**\n" +
                   "  Calle 45 # 12 - 34, Bogotá D.C., Colombia.\n\n" +
                   "📱 **Líneas Telefónicas y WhatsApp:**\n" +
                   "  • WhatsApp: (+57) 310 123 4567\n" +
                   "  • Atención al cliente: (601) 765 4321\n\n" +
                   "✉️ **Correos de Contacto:**\n" +
                   "  • contacto@carpintec.com\n" +
                   "  • info@carpintec.com\n\n" +
                   "⏰ **Horarios de Atención:**\n" +
                   "  • Lunes a Viernes: 8:00 a.m. a 6:00 p.m.\n" +
                   "  • Sábados: 8:00 a.m. a 2:00 p.m.\n" +
                   "  • Domingos y Festivos: No laboramos.\n\n" +
                   "💬 También puedes llenar el formulario en la sección **[Contacto](#contacto-form)** de esta misma página web.";
        }

        private static string GenerarPreguntasFrecuentes()
        {
            return "❓ **Preguntas Frecuentes de Nuestros Clientes (FAQ)**\n\n" +
                   "• **¿Cuánto tiempo toma fabricar un mueble?**\n" +
                   "  El tiempo promedio oscila entre 15 y 25 días hábiles, dependiendo del tamaño y complejidad del mobiliario.\n\n" +
                   "• **¿Los muebles cuentan con garantía?**\n" +
                   "  Sí, ofrecemos de 1 a 3 años de garantía en estructura y herrajes contra defectos de fabricación.\n\n" +
                   "• **¿Hacen entregas e instalación en el domicilio?**\n" +
                   "  Sí, contamos con equipo de transporte especializado y técnicos montadores profesionales que dejan el mueble instalado y listo para usar.\n\n" +
                   "• **¿Cuáles son los métodos de pago aceptados?**\n" +
                   "  Aceptamos transferencias bancarias, tarjetas de débito/crédito y pagos en efectivo.\n\n" +
                   "• **¿Cómo solicito una cotización formal?**\n" +
                   "  Por seguridad y seguimiento, debes crear una cuenta gratuita en **[Crear cuenta](/Registro)** e iniciar sesión para enviar tus medidas y especificaciones.";
        }

        private static string GenerarGuiaNavegacion()
        {
            return "🧭 **Guía de Navegación por la Página Web de CARPINTEC**\n\n" +
                   "Encuentra rápidamente lo que buscas en nuestro sitio:\n\n" +
                   "• **[Inicio](#inicio):** Portada principal con destacados y novedades.\n" +
                   "• **[Acerca de](#acerca-de):** Conoce nuestra historia y dedicación a la madera.\n" +
                   "• **[Servicios](#servicios):** Revisa nuestras áreas de cocinas, closets y muebles con sus catálogos interactivos.\n" +
                   "• **[Trabajos](#trabajos):** Galería fotográfica de proyectos elaborados por nuestro taller.\n" +
                   "• **[Contacto](#contacto-form):** Envíanos un mensaje rápido con tus datos.\n" +
                   "• **[Crear Cuenta](/Registro):** Regístrate como cliente para poder cotizar y hacer pedidos.\n" +
                   "• **[Iniciar Sesión](/Login):** Accede a tu cuenta personal para ver el avance de tus cotizaciones y pedidos.";
        }

        private static string GenerarRespuestaPorDefecto(string pregunta)
        {
            return $"💬 No logré comprender del todo tu consulta: *\"{pregunta}\"*.\n\n" +
                   "Como asesor de atención al cliente de **CARPINTEC**, puedo ayudarte con los siguientes temas:\n\n" +
                   "• 🏢 *'¿Quiénes somos?'* (misión, visión y taller artesanal)\n" +
                   "• 🪵 *'Nuestros servicios'* (cocinas, closets, muebles y materiales)\n" +
                   "• 📞 *'Contacto'* (teléfono, WhatsApp, dirección y horarios)\n" +
                   "• ❓ *'Preguntas frecuentes'* (tiempos, garantías e instalación)\n" +
                   "• 📄 *'Solicitar cotización'* (información para cotizar tus muebles)\n\n" +
                   "📋 Recuerda que si deseas cotizar un proyecto formal o solicitar un pedido, debes **[Crear una cuenta](/Registro)** o **[Iniciar sesión](/Login)**.";
        }

        #endregion
    }
}
