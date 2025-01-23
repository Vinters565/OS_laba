//Задание:
//Разработать модель имитация файловой системы, основанной на FAT таблице.
//Должна быть структура данных для таблицы. Имитация списка свободных и занятых кластеров.
//При создании модели файла - создается создается запись в таблице, к этой записи подключаются дополнительные кластера.
//Полученная модель должна быть потокобезопасна.
//Для тестирования - создать несколько потоков которые будут создавать файлы и имитировать работу с ними.


//Результат
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class FatFileSystem
{
    private readonly int _clusterCount;
    private readonly object _lock = new object();

    // Таблица FAT: Сопоставляет индекс кластера со следующим кластером или -1, если это последний кластер в цепочке.
    private readonly int[] _fatTable;

    // Отслеживает свободные кластеры
    private readonly ConcurrentQueue<int> _freeClusters;

    // Представляет файлы в файловой системе (имя файла для начального сопоставления кластера)
    private readonly ConcurrentDictionary<string, int> _files;

    public FatFileSystem(int clusterCount)
    {
        if (clusterCount <= 0)
            throw new ArgumentException("Cluster count must be greater than zero.");

        _clusterCount = clusterCount;
        _fatTable = new int[_clusterCount];
        _freeClusters = new ConcurrentQueue<int>(Enumerable.Range(0, _clusterCount));
        _files = new ConcurrentDictionary<string, int>();

        // Инициализируйте таблицу FAT значением -1 (изначально все кластеры свободны)
        for (int i = 0; i < _clusterCount; i++)
        {
            _fatTable[i] = -1;
        }
    }

    public bool CreateFile(string fileName, int clusterCount)
    {
        if (_files.ContainsKey(fileName))
        {
            Console.WriteLine($"File '{fileName}' already exists.");
            return false;
        }

        var allocatedClusters = new List<int>();
        for (int i = 0; i < clusterCount; i++)
        {
            if (!_freeClusters.TryDequeue(out int cluster))
            {
                Console.WriteLine("Not enough free clusters.");
                FreeClusters(allocatedClusters);
                return false;
            }
            allocatedClusters.Add(cluster);
        }

        // Кластеры ссылок в таблице FAT
        for (int i = 0; i < allocatedClusters.Count - 1; i++)
        {
            _fatTable[allocatedClusters[i]] = allocatedClusters[i + 1];
        }

        // Отмечает последний кластер в цепочке
        _fatTable[allocatedClusters[^1]] = -1;

        // Добавляет файл в файловый словарь
        _files[fileName] = allocatedClusters[0];
        Console.WriteLine($"File '{fileName}' created with clusters: {string.Join(", ", allocatedClusters)}.");

        return true;
    }

    public void DeleteFile(string fileName)
    {
        if (!_files.TryRemove(fileName, out int startCluster))
        {
            Console.WriteLine($"File '{fileName}' not found.");
            return;
        }

        // Освобождает все кластеры, используемые файлом
        var currentCluster = startCluster;
        while (currentCluster != -1)
        {
            var nextCluster = _fatTable[currentCluster];
            _fatTable[currentCluster] = -1;
            _freeClusters.Enqueue(currentCluster);
            currentCluster = nextCluster;
        }

        Console.WriteLine($"File '{fileName}' deleted and clusters freed.");
    }

    public void PrintFatTable()
    {
        lock (_lock)
        {
            Console.WriteLine("FAT Table:");
            for (int i = 0; i < _fatTable.Length; i++)
            {
                Console.WriteLine($"Cluster {i}: {_fatTable[i]}");
            }
        }
    }

    private void FreeClusters(IEnumerable<int> clusters)
    {
        foreach (var cluster in clusters)
        {
            _freeClusters.Enqueue(cluster);
        }
    }

    public void PrintFileSystem()
    {
        lock (_lock)
        {
            Console.WriteLine("File System State:");
            foreach (var file in _files)
            {
                Console.WriteLine($"File: {file.Key}, Start Cluster: {file.Value}");
            }
        }
    }
}

//Имитируем/Тестируем работу
public class Program
{
    public static void Main()
    {
        var fatFileSystem = new FatFileSystem(100);

        // Запускаем несколько потоков для создания и удаления файлов
        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            int threadIndex = i;
            tasks.Add(Task.Run(() =>
            {
                var fileName = $"File_{threadIndex}";
                if (fatFileSystem.CreateFile(fileName, 5))
                {
                    Thread.Sleep(new Random().Next(100, 500));
                    fatFileSystem.DeleteFile(fileName);
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Выводит конечное состояние таблицы и файловой системы FAT
        fatFileSystem.PrintFatTable();
        fatFileSystem.PrintFileSystem();
    }
}
