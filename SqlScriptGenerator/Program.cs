using System;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Security.Principal;

class Program
{
    static void Main(string[] args)
    {
        string serverName = "";
        string databaseName = "";
        string countRows = "";
        string useUserAndPassword = "";
        string connectionString = "";
        string user = "";
        string password = "";
        do
        {
            Console.Write("Ingrese ServerName: ");
            serverName = Console.ReadLine();
        } while (string.IsNullOrEmpty(serverName));
        do
        {
            Console.Write("Ingrese nombre de la tabla: ");
            databaseName = Console.ReadLine();
        } while (string.IsNullOrEmpty(databaseName));
        do
        {
            Console.Write("Ingrese cantidad de fila a guardar: ");
            countRows = Console.ReadLine();
        } while (string.IsNullOrEmpty(countRows));

        do
        {
            Console.Write("Tiene usuario y password? (si/no): ");

            useUserAndPassword = Console.ReadLine();
        } while (useUserAndPassword.ToLower() != "sí" && useUserAndPassword.ToLower() != "no");

        if (useUserAndPassword.ToLower() == "si")
        {
            do
            {
                Console.Write("Ingrese nombre de usuario: ");
                user = Console.ReadLine();
            } while (string.IsNullOrEmpty(user));
            do
            {
                Console.Write("Ingrese la password: ");
                password = Console.ReadLine();
            } while (string.IsNullOrEmpty(password));

            connectionString = $"Data Source={serverName};Initial Catalog={databaseName};User ID={user};Password={password};MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;";
        }
        else
        {
            connectionString = $"Data Source={serverName};Initial Catalog={databaseName};Trusted_Connection=True;MultipleActiveResultSets=True;";
        }
        try
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Obtener lista de tablas
                SqlCommand command = new SqlCommand("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_SCHEMA = 'dbo'", connection);
                SqlDataReader reader = command.ExecuteReader();

                // Generar script SQL con los inserts de las filas seleccionadas
                string script = "";
                while (reader.Read())
                {
                    string tableName = reader.GetString(0);

                    // Obtener primeras x filas
                    command = new SqlCommand($"SELECT TOP {countRows} * FROM {tableName}", connection);
                    SqlDataReader dataReader = command.ExecuteReader();

                    if (dataReader.HasRows)
                    {
                        // Obtener información de la columna de identidad
                        command = new SqlCommand($"SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo' AND COLUMNPROPERTY(OBJECT_ID(TABLE_NAME), COLUMN_NAME, 'IsIdentity') = 1 AND TABLE_NAME = '{tableName}'", connection);

                        object identityResult = command.ExecuteScalar();

                        bool isIdentity = false;

                        if (identityResult != null && identityResult != DBNull.Value)
                        {
                            isIdentity = true;
                        }

                        if (isIdentity)
                        {
                            script += $"SET IDENTITY_INSERT {tableName} ON;{Environment.NewLine}";
                        }

                        while (dataReader.Read())
                        {
                            string columns = "";
                            string values = "";
                            for (int i = 0; i < dataReader.FieldCount; i++)
                            {
                                if (!dataReader.IsDBNull(i))
                                {
                                    if (columns != "")
                                    {
                                        columns += ", ";
                                        values += ", ";
                                    }
                                    columns += $"[{dataReader.GetName(i)}]";
                                    var obj = dataReader.GetValue(i);
                                    if (obj is DateTime)
                                    {
                                        DateTime date = DateTime.Parse(obj.ToString());

                                        values += $"'{date.ToString("yyyy/MM/dd").Replace("'", "''")}'";

                                    }
                                    else
                                    {
                                        values += $"'{dataReader.GetValue(i).ToString().Replace("'", "''")}'";
                                    }
                                }
                            }
                            script += $"INSERT INTO {tableName} ({columns}) VALUES ({values});{Environment.NewLine}";
                        }
                        if (isIdentity)
                        {
                            script += $"SET IDENTITY_INSERT {tableName} OFF;{Environment.NewLine}";
                        }
                    }

                    dataReader.Close();
                }

                reader.Close();

                // Escribir script SQL a archivo
                File.WriteAllText($"{serverName}__{databaseName}_Data.sql", script);

                Console.WriteLine("Done!");
                Console.ReadKey();
            }
        }
        catch (Exception)
        {
            Console.WriteLine("No se pudo generar el script, ocurrió un error.");
            Console.ReadKey();
        }

    }
}
