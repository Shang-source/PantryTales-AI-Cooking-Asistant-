using System;
using Microsoft.EntityFrameworkCore;

namespace backend.Extensions;

public static class PostgresFunctions
{
    [DbFunction("md5", IsBuiltIn = true)]
    public static string Md5(string input) => throw new NotSupportedException();

    [DbFunction("concat", IsBuiltIn = true)]
    public static string Concat(Guid left, string right) => throw new NotSupportedException();
}
