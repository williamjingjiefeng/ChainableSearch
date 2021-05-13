using System;
using System.Collections.Generic;
using System.Reflection;

namespace ChainableSearch.Services
{
    public class PropertyMetaData
    {
        public List<PropertyInfo> PropertyInfoList { get; set; }
        public Type Type { get; set; }
    }
}
