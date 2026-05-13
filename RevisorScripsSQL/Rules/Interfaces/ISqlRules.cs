using System;
using System.Collections.Generic;
using System.Text;
using RevisorScripstSQL.Models;

namespace RevisorScripstSQL.Rules.Interfaces
{
    public interface ISqlRules
    {
        List<SqlError> Validate(string[] lineas);
    }
}