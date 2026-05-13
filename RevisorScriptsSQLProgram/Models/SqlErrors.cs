using System;
using System.Collections.Generic;
using System.Text;

namespace RevisorScripstSQL.Models
{
    public class SqlError
    {
        public int Linea { get; set; }
        public string Regla { get; set; }
        public string Mensaje { get; set; }
        public string Correccion { get; set; }
        public string CodigoLinea { get; set; }
    }
}
