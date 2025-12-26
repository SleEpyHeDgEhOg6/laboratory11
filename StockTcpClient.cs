using System;
using System.Net.Sockets;
using System.Text;

namespace StockTcpClient
{
    class Program
    {
        static void Main()
        {
            Console.WriteLine("      Клиент       ");
            
            Console.WriteLine("\n Адрес сервера: localhost:5000");
            Console.WriteLine(" Сервер использует БД из анализатора акций");
            Console.WriteLine(" Введите 'help' для справки");
            Console.WriteLine(" Введите 'exit' для выхода");
            Console.WriteLine(new string('-', 50));

            while (true)
            {
                Console.Write("\n Тикер - ");
                var input = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(input)) continue;
                
                if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                    input.Equals("quit", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("\n До свидания!");
                    break;
                }

                if (input.Equals("help", StringComparison.OrdinalIgnoreCase) ||
                    input.Equals("?", StringComparison.OrdinalIgnoreCase))
                {
                    ShowHelp();
                    continue;
                }

                if (input.Equals("test", StringComparison.OrdinalIgnoreCase))
                {
                    TestConnection();
                    continue;
                }

                GetStockPrice(input);
            }
        }

        static void ShowHelp()
        {
            Console.WriteLine("\n Команды клиента:");
            Console.WriteLine("  help, ?    - показать эту справку");
            Console.WriteLine("  test       - проверить подключение к серверу");
            Console.WriteLine("  exit, quit - выйти из программы");
            Console.WriteLine("\n Пример запроса :");
            Console.WriteLine("  Введите тикер акции (например: AAPL)");
            Console.WriteLine("  Сервер вернет последнюю цену из БД");
        }

        static void TestConnection()
        {
            Console.WriteLine("\n Проверка подключения к серверу...");
            
            try
            {
                using var client = new TcpClient();
                
                // Пробуем подключиться с таймаутом 2 секунды
                var connectTask = client.ConnectAsync("127.0.0.1", 5000);
                
                if (connectTask.Wait(2000))
                {
                    Console.WriteLine(" Сервер доступен!");
                    
                    // Проверяем отвечает ли сервер
                    using var stream = client.GetStream();
                    var testData = Encoding.UTF8.GetBytes("TEST");
                    stream.Write(testData, 0, testData.Length);
                    
                    var buffer = new byte[256];
                    var bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        Console.WriteLine($" Сервер ответил: {response}");
                    }
                }
                else
                {
                    Console.WriteLine(" Сервер не отвечает (таймаут)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($" Ошибка подключения: {ex.Message}");
            }
        }

        static void GetStockPrice(string symbol)
        {
            try
            {
                Console.WriteLine($"\n Подключаюсь к серверу...");
                
                using var client = new TcpClient();
                
                if (!client.ConnectAsync("127.0.0.1", 5000).Wait(3000))
                {
                    Console.WriteLine("  Сервер не отвечает (таймаут)");
                    return;
                }

                Console.WriteLine($"Подключено! Запрашиваем: {symbol.ToUpper()}");
                
                using var stream = client.GetStream();
                
                var requestData = Encoding.UTF8.GetBytes(symbol.ToUpper()); //отправляем запрос 
                stream.Write(requestData, 0, requestData.Length);
                
                var buffer = new byte[1024]; //получаем ответ от сервера 
                var bytesRead = stream.Read(buffer, 0, buffer.Length);
                
                if (bytesRead > 0)
                {
                    var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    
                    Console.WriteLine("\n" + new string('━', 50));
                    Console.WriteLine("Результат запроса:");
                    Console.WriteLine(new string('━', 50));
                    
                    if (response.Contains("не найден"))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"   {response}");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"   {response}");
                        Console.ResetColor();
                    }
                    
                    Console.WriteLine(new string('━', 50));
                }
                else
                {
                    Console.WriteLine("  Сервер не вернул ответ");
                }
            }
            catch (SocketException)
            {
                Console.WriteLine(" Не удалось подключиться к серверу");
            }
            catch (Exception ex)
            {
                Console.WriteLine($" Неожиданная ошибка: {ex.Message}");
            }
        }
    }
}
