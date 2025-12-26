using System;
using System.Data.SQLite;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace StockTcpServer
{
    class Program
    {
        static void Main()
        {
            Console.WriteLine("       Сервер     ");

            string dbPath = "/home/vlada/RiderProjects/lab110/lab110/bin/Debug/net8.0/stocks.db";
            
            if (!System.IO.File.Exists(dbPath))
            {
                Console.WriteLine("\n Файл базы данных не найден!");
                Console.WriteLine("\n Сначала запустите анализатор акций");
                Console.WriteLine("\n После этого запустите сервер снова.");
                Console.WriteLine("\nНажмите Enter для выхода...");
                Console.ReadLine();
                return;
            }
            
            Console.WriteLine(" База данных найдена!");
            
            try
            {
                var server = new StockServer(dbPath);
                server.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n Ошибка запуска сервера: {ex.Message}");
                Console.WriteLine("\nНажмите Enter для выхода...");
                Console.ReadLine();
            }
        }
    }

    public class StockServer
    {
        private readonly TcpListener _listener;
        private readonly string _dbPath;

        public StockServer(string dbPath)
        {
            _listener = new TcpListener(IPAddress.Loopback, 5000);
            _dbPath = dbPath;
        }

        public void Start()
        {
            try
            {
                _listener.Start();
                Console.WriteLine("\n Сервер запущен!");
                Console.WriteLine(" Адрес: localhost:5000");
                Console.WriteLine(" Ожидание подключений...");
                Console.WriteLine("\n Для остановки закройте это окно");
                Console.WriteLine(new string('-', 50));

                while (true)
                {
                    var client = _listener.AcceptTcpClient();
                    Console.WriteLine($"\n Новый клиент подключился");
                    HandleClient(client);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n Ошибка сервера: {ex.Message}");
            }
        }

        private void HandleClient(TcpClient client) //обработка клиентского запроса 
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    var buffer = new byte[256]; //буфер для чтения данных от клиента 
                    var bytesRead = stream.Read(buffer, 0, buffer.Length);
                    
                    if (bytesRead == 0)
                    {
                        Console.WriteLine("  Пустой запрос от клиента");
                        return;
                    }
                    
                    var symbol = Encoding.UTF8.GetString(buffer, 0, bytesRead) //преобразуем байты в строку , убираем пробелы и приводим к регистру 
                                             .Trim()
                                             .ToUpper();
                    
                    Console.WriteLine($" Получен запрос: {symbol}");
                    
                    var price = GetLatestPrice(symbol);
                    
                    var condition = GetTodayCondition(symbol);
                    
                    string response;
                    
                    if (price.HasValue)  //формируем ответ 
                    {
                        response = $"{symbol}: ${price:F2}";
                        if (!string.IsNullOrEmpty(condition))
                        {
                            response += $" ({condition})";
                        }
                        Console.WriteLine($" Отправлен ответ: {response}");
                    }
                    else
                    {
                        response = $"Тикер '{symbol}' не найден в базе";
                        Console.WriteLine($"  {response}");
                    }

                    var responseData = Encoding.UTF8.GetBytes(response);
                    stream.Write(responseData, 0, responseData.Length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($" Ошибка обработки клиента: {ex.Message}");
            }
        }

        private double? GetLatestPrice(string symbol)
        {
            try
            {
                using var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
                connection.Open();
//SQL запрос с JOIN таблиц
                var sql = @"        
                    SELECT p.Value 
                    FROM Prices p
                    JOIN Tickers t ON p.TickerId = t.Id
                    WHERE t.Symbol = @Symbol
                    ORDER BY p.Date DESC
                    LIMIT 1"; //берем только последнюю запись 

                using var command = new SQLiteCommand(sql, connection);
                command.Parameters.AddWithValue("@Symbol", symbol);

                var result = command.ExecuteScalar(); //возвращает первое значение первой строки 
                
                if (result == DBNull.Value || result == null)
                    return null;
                    
                return Convert.ToDouble(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($" Ошибка запроса цены: {ex.Message}");
                return null;
            }
        }

        private string? GetTodayCondition(string symbol)
        {
            try
            {
                using var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
                connection.Open();

                var sql = @"
                    SELECT tc.State 
                    FROM TodaysConditions tc
                    JOIN Tickers t ON tc.TickerId = t.Id
                    WHERE t.Symbol = @Symbol";

                using var command = new SQLiteCommand(sql, connection);
                command.Parameters.AddWithValue("@Symbol", symbol);

                var result = command.ExecuteScalar();
                
                if (result == DBNull.Value || result == null)
                    return null;
                    
                return result.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка запроса состояния: {ex.Message}");
                return null;
            }
        }

        public void ShowAvailableTickers()
        {
            try
            {
                using var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
                connection.Open();

                var sql = @"
                    SELECT t.Symbol, 
                           (SELECT p.Value FROM Prices p 
                            WHERE p.TickerId = t.Id 
                            ORDER BY p.Date DESC LIMIT 1) as LastPrice,
                           tc.State
                    FROM Tickers t
                    LEFT JOIN Prices p ON t.Id = p.TickerId
                    LEFT JOIN TodaysConditions tc ON t.Id = tc.TickerId
                    ORDER BY t.Symbol";

                using var command = new SQLiteCommand(sql, connection);
                using var reader = command.ExecuteReader();
                
                Console.WriteLine("\nДоступные акции:");
                Console.WriteLine(new string('─', 40));
                
                bool hasData = false;
                while (reader.Read())
                {
                    hasData = true;
                    var ticker = reader.GetString(0);
                    var price = reader.IsDBNull(1) ? "нет данных" : $"${reader.GetDouble(1):F2}";
                    var state = reader.IsDBNull(2) ? "" : $" [{reader.GetString(2)}]";
                    Console.WriteLine($"  {ticker,-6} - {price}{state}");
                }
                
                if (!hasData)
                {
                    Console.WriteLine("  (база данных пуста)");
                }
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($" Не удалось прочитать список: {ex.Message}");
            }
        }
    }
}
