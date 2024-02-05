using System.Data.SqlClient;
using System.Text;

namespace DiffDB
{
    public class DiffRepository(string connectionStringDestino, string connectionStringOrigem)
    {

        public bool TabelaExiste(string tabela)
        {
            string tabelaExiste = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @TableName";
            using SqlConnection conn = new(connectionStringDestino);
            conn.Open();

            using SqlCommand command = conn.CreateCommand();
            command.CommandText = tabelaExiste;
            command.Parameters.AddWithValue("@TableName", tabela);
            int count = (int)command.ExecuteScalar();

            return count > 0;
        }

        public string GerarScriptCreateTable(string table)
        {
            StringBuilder scriptBuilder = new();

            using SqlConnection connection = new(connectionStringOrigem);
            connection.Open();

            string query = @$"SELECT 
                                    COLUMN_NAME, 
                                    DATA_TYPE, 
                                    CHARACTER_MAXIMUM_LENGTH, 
                                    IS_NULLABLE 
                                FROM 
                                    INFORMATION_SCHEMA.COLUMNS 
                                WHERE 
                                    TABLE_NAME = '{table}'";

            SqlCommand command = new(query, connection);
            SqlDataReader reader = command.ExecuteReader();

            scriptBuilder.AppendLine($"--Gerando script de criação da {table}");
            scriptBuilder.AppendLine($"if not exists(select * from sys.objects where name = '{table}')");
            scriptBuilder.AppendLine($"BEGIN");
            scriptBuilder.AppendLine($"CREATE TABLE [{table}] (");

            while (reader.Read())
            {
                string? columnName = reader["COLUMN_NAME"].ToString();
                string? dataType = reader["DATA_TYPE"].ToString();
                string? maxLength = reader["CHARACTER_MAXIMUM_LENGTH"].ToString();
                bool isNullable = reader["IS_NULLABLE"].ToString() == "YES";

                string columnDefinition = $"    [{columnName}] {dataType}";

                if (!string.IsNullOrEmpty(maxLength))
                {
                    columnDefinition += $"({maxLength})";
                }

                columnDefinition += isNullable ? " NULL," : " NOT NULL,";
                scriptBuilder.AppendLine(columnDefinition);
            }

            reader.Close();

            string scriptFinal = scriptBuilder.ToString();
            scriptFinal = scriptFinal.Remove(scriptFinal.Length - 3);
            return scriptFinal + "\n);\nEND;\n";
        }

        public static string GeraScriptsAlterTable(string connectionString, string tableName)
        {
            StringBuilder scripts = new();

            using SqlConnection connection = new(connectionString);
            connection.Open();

            string query = @$"SELECT 
                                    TABLE_NAME,
                                    COLUMN_NAME, 
                                    DATA_TYPE, 
                                    CHARACTER_MAXIMUM_LENGTH, 
                                    IS_NULLABLE 
                              FROM
                                    INFORMATION_SCHEMA.COLUMNS
                              WHERE 
                                    TABLE_NAME = '{tableName}'";

            using SqlCommand command = new(query, connection);
            using SqlDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                string? table = reader["TABLE_NAME"].ToString();
                string? columnName = reader["COLUMN_NAME"].ToString();
                string? dataType = reader["DATA_TYPE"].ToString();
                string? maxLength = reader["CHARACTER_MAXIMUM_LENGTH"].ToString();
                string? sizeOfField = maxLength is not null && maxLength.Length > 0 ? $"({maxLength})" : "";
                bool isNullable = reader["IS_NULLABLE"].ToString() == "YES";
                string renderNull = isNullable ? "NULL" : "NOT NULL";

                if (columnName is not null
                    && table is not null
                    && dataType is not null)
                {
                    scripts.AppendLine($"--SCRIPT DE ALTERAÇÃO DA TABELA {table}");
                    scripts.AppendLine("IF NOT EXISTS(select * from sys.columns");
                    scripts.AppendLine($"                           where object_id = OBJECT_ID('{table}')");
                    scripts.AppendLine($"                           and name = '{columnName}')");
                    scripts.AppendLine($"BEGIN");
                    scripts.AppendLine($"   ALTER TABLE {table} ADD {columnName} {dataType}{sizeOfField} {renderNull}");
                    scripts.AppendLine($"END");
                    scripts.AppendLine();
                }
            }

            reader.Close();
            return scripts.ToString();

        }

        public List<string> ListarTodasAsTabelasDaBase()
        {
            using SqlConnection connection = new(connectionStringOrigem);
            connection.Open();
            List<string> tabelas = [];
            SqlCommand command = new("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'", connection);
            SqlDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                string? _tabela = reader["TABLE_NAME"].ToString();

                if (_tabela is not null)
                    tabelas.Add(_tabela);
            }

            reader.Close();
            return tabelas;
        }

        public string GerarScriptConstraintsSemFK(string tableName){
            StringBuilder scripts = new();

            using SqlConnection connection = new(connectionStringOrigem);
            connection.Open();

            string query = @$"SELECT
                                    TC.TABLE_NAME,
                                    CONSTRAINT_TYPE,
                                    COLUMN_NAME
                                FROM 
                                    INFORMATION_SCHEMA.TABLE_CONSTRAINTS TC
                                JOIN 
                                    INFORMATION_SCHEMA.KEY_COLUMN_USAGE KCU
                                ON 
                                    TC.CONSTRAINT_NAME = KCU.CONSTRAINT_NAME
                                WHERE 
                                    TC.TABLE_NAME '{tableName}'
                                    AND CONSTRAINT_TYPE != 'FOREIGN KEY'";

            using SqlCommand command = new(query, connection);
            using SqlDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                string? table = reader["TABLE_NAME"].ToString();
                string? constraintType = reader["CONSTRAINT_TYPE"].ToString();
                string? columnName = reader["COLUMN_NAME"].ToString();

                if (constraintType is not null
                    && table is not null
                    && columnName is not null)
                {
                    scripts.AppendLine($"--SCRIPT DE ALTERAÇÃO DA TABELA {table}");
                    scripts.AppendLine("IF NOT EXISTS(select * from INFORMATION_SCHEMA.TABLE_CONSTRAINTS TC JOIN");
                    scripts.AppendLine("INFORMATION_SCHEMA.KEY_COLUMN_USAGE KCU");
                    scripts.AppendLine("ON TC.CONSTRAINT_NAME = KCU.CONSTRAINT_NAME");
                    scripts.AppendLine($"       where     TC.TABLE_NAME = '{table}'");
                    scripts.AppendLine($"       AND COLUMN_NAME = '{columnName}' ");
                    scripts.AppendLine($"       AND CONSTRAINT_TYPE != 'FOREIGN KEY'");
                    
                    scripts.AppendLine($"BEGIN");
                    scripts.AppendLine($"   ALTER TABLE {table} ADD {constraintType} ({columnName})");
                    scripts.AppendLine($"END");
                    scripts.AppendLine();
                }
            }

            reader.Close();
            return scripts.ToString();


        }

        // public string GerarScriptTodasPKs(string[] tabelas)
        // {
        //     using SqlConnection connection = new(connectionStringOrigem);
        //     connection.Open();
        //     List<string> script = [];
        //     SqlCommand command = new("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'", connection);
        //     SqlDataReader reader = command.ExecuteReader();

        //     while (reader.Read())
        //     {
        //         string? _tabela = reader["TABLE_NAME"].ToString();

        //         if (_tabela is not null)
        //             tabelas.Add(_tabela);
        //     }

        //     reader.Close();
        //     return tabelas;
        // }

    }
}