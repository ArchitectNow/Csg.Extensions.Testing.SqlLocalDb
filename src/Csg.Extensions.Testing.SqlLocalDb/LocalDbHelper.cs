using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace Csg.Extensions.Testing.SqlLocalDb;

public static class LocalDbHelper
{
    public const string DataPath = @"%LOCALAPPDATA%\LocalDb";
    private const string LocalDbExe = "sqllocaldb.exe";

    [UsedImplicitly]
    public static void CreateInstance(string instanceName)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException();
        
        Exec(LocalDbExe, $"create {instanceName} -s");
        var dataPath = System.IO.Path.Combine(Environment.ExpandEnvironmentVariables(DataPath), instanceName);

        if (!System.IO.Directory.Exists(dataPath))
        {
            System.IO.Directory.CreateDirectory(dataPath);
        }
    }

    [UsedImplicitly]
    public static string CreateDatabase(string dbName, string instanceName)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException();
        
        Exec("sqllocaldb.exe", $"create {instanceName} -s");

        var connectionString = $"Data Source=(localdb)\\{instanceName};Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False;ApplicationIntent=ReadWrite;MultiSubnetFailover=False";
        var dataPath = System.IO.Path.Combine(Environment.ExpandEnvironmentVariables(DataPath), instanceName);

        using var conn = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
        
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
                           IF EXISTS (SELECT 1 FROM sys.databases WHERE [name]='{dbName}') 
                           DROP DATABASE [{dbName}]; 
                           CREATE DATABASE [{dbName}] 
                           ON PRIMARY ( Name = {dbName}_data, FILENAME = '{dataPath}\{dbName}_data.mdf')
                           LOG ON ( Name = {dbName}_log, FILENAME = '{dataPath}\{dbName}_log.ldf')
                           ;
                           """;

        cmd.ExecuteNonQuery();
        

        connectionString = string.Concat(connectionString, $";Initial Catalog={dbName}");

        return connectionString;
    }
    
    [UsedImplicitly]
    public static void DeleteDatabase(string databaseName, string instanceName)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException();
        
        var connectionString = $"Data Source=(localdb)\\{instanceName};Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False;ApplicationIntent=ReadWrite;MultiSubnetFailover=False";

        using var conn = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
                           IF EXISTS (SELECT 1 FROM sys.databases WHERE [name]='{databaseName}') BEGIN
                               ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                               DROP DATABASE [{databaseName}];
                           END
                           """;
        cmd.ExecuteNonQuery();
    }

    [UsedImplicitly]
    public static void StopInstance(string instanceName)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException();
        
        var resultCode = Exec(LocalDbExe, $"stop {instanceName} -k");

        if (resultCode != 0)
        {
            throw new Exception($"sqllocaldb.exe stop result code {resultCode}");
        }
    }

    [UsedImplicitly]
    public static void DeleteInstance(string instanceName)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException();
        
        StopInstance(instanceName);

        var resultCode = Exec(LocalDbExe, $"delete {instanceName}");
        if (resultCode != 0)
        {
            throw new Exception($"sqllocaldb.exe delete result code {resultCode}");
        }

        var dataPath = System.IO.Path.Combine(Environment.ExpandEnvironmentVariables(DataPath), instanceName);
        var files = System.IO.Directory.GetFiles(dataPath);
        foreach (var file in files)
        {
            System.IO.File.Delete(file);
        }
    }

    [UsedImplicitly]
    public static bool TryDeleteInstance(string instanceName)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException();
        
        try
        {
            DeleteInstance(instanceName);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    private static int Exec(string cmd, string arguments, bool useCmd = true, System.IO.StreamWriter? output = null)
    {
        var fileName = useCmd ? "cmd.exe" : cmd;
        var args = useCmd ? string.Concat("/c ", cmd, " ",arguments): arguments;

        if (output == null)
        {
            Debug.WriteLine($"Executing {fileName} {args}");
        }
        else
        {
            output.WriteLine($"Executing {fileName} {args}");
        }

        var processInfo = new ProcessStartInfo(fileName, args)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        var process = Process.Start(processInfo);

        if (process is null)
        {
            Debug.WriteLine("Unable to Start Process");
            return -1;
        }

        process.WaitForExit();

        var processOutput = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();

        var exitCode = process.ExitCode;

        if (output == null)
        {
            Debug.WriteLine(processOutput);
            Debug.WriteLine(error);
        }
        else
        {
            output.WriteLine(processOutput);
            output.WriteLine(error);
        }

        process.Close();

        return exitCode;
    }
}