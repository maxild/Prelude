using System;
using System.Reflection;

[assembly: CLSCompliant(true)]

#if NETSTANDARD1_0
[assembly: AssemblyTitle("Maxfire.Prelude.ComponentModel.TypeConverter .NET Standard 1.0")]
#elif NETSTANDARD1_1
[assembly: AssemblyTitle("Maxfire.Prelude.ComponentModel.TypeConverter .NET Standard 1.1")]
#elif NETSTANDARD1_2
[assembly: AssemblyTitle("Maxfire.Prelude.ComponentModel.TypeConverter .NET Standard 1.2")]
#elif NETSTANDARD1_3
[assembly: AssemblyTitle("Maxfire.Prelude.ComponentModel.TypeConverter .NET Standard 1.3")]
#elif NETSTANDARD1_4
[assembly: AssemblyTitle("Maxfire.Prelude.ComponentModel.TypeConverter .NET Standard 1.4")]
#elif NETSTANDARD1_5
[assembly: AssemblyTitle("Maxfire.Prelude.ComponentModel.TypeConverter .NET Standard 1.5")]
#elif NETSTANDARD1_6
[assembly: AssemblyTitle("Maxfire.Prelude.ComponentModel.TypeConverter .NET Standard 1.6")]
#elif NET45
[assembly: AssemblyTitle("Maxfire.Prelude.ComponentModel.TypeConverter .NET Framework 4.5")]
#elif NET451
[assembly: AssemblyTitle("Maxfire.Prelude.ComponentModel.TypeConverter .NET Framework 4.5.1")]
#elif NET452
[assembly: AssemblyTitle("Maxfire.Prelude.ComponentModel.TypeConverter .NET Framework 4.5.2")]
#elif NET46
[assembly: AssemblyTitle("Maxfire.Prelude.ComponentModel.TypeConverter .NET Framework 4.6")]
#elif NET461
[assembly: AssemblyTitle("Maxfire.Prelude.ComponentModel.TypeConverter .NET Framework 4.6.1")]
#elif NET462
[assembly: AssemblyTitle("Maxfire.Prelude.ComponentModel.TypeConverter .NET Framework 4.6.2")]
#else
[assembly: AssemblyTitle("Maxfire.Prelude.ComponentModel.TypeConverter")]
#endif
