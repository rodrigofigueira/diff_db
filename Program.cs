using DiffDB;

Console.WriteLine("Iniciando comparação de Bases de Dados");

string connectionStringOrigem = "Data Source=10.30.3.60;Initial Catalog=dbDetranNetAtelier;User ID=UserApiCore;Password=detranR03332###@;";
string connectionStringDestino = "Data Source=10.30.3.60;Initial Catalog=dbDetranNet;User ID=UserApiCore;Password=detranR03332###@;";
string script_equalizacao = "script__equalizacao.txt";

string arquivoComNomesDasTabelas = "tabelas.txt";
string[] tabelas = File.ReadAllLines(arquivoComNomesDasTabelas);

DiffRepository diffRepository = new(connectionStringDestino, connectionStringOrigem);

//Essa linha gera toda a tabela
//List<string> tabelas = diffRepository.ListarTodasAsTabelasDaBase();

foreach (string tabela in tabelas)
{

    Console.WriteLine($"Consultando objeto {tabela}");

    // if (diffRepository.TabelaExiste(tabela) == false)
    // {
    //     using StreamWriter writer = File.AppendText(script_equalizacao);
    //     writer.WriteLine($"--Gerando script de criação para a tabela {tabela}");
    //     writer.WriteLine(diffRepository.GerarScriptCreateTable(tabela));
    // }
    // else
    // {
    //     using StreamWriter writer = File.AppendText(script_equalizacao);
    //     writer.WriteLine(DiffRepository.GeraScriptsAlterTable(connectionStringOrigem, tabela));
    // }

    using StreamWriter writer = File.AppendText(script_equalizacao);
    writer.WriteLine(diffRepository.GerarScriptCreateTable(tabela));
    writer.WriteLine(DiffRepository.GeraScriptsAlterTable(connectionStringOrigem, tabela));
    writer.WriteLine(diffRepository.GerarScriptConstraintsSemFK(tabela));
}

Console.WriteLine("Fim da rotina de comparação");
