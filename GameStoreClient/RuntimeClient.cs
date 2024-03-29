﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Domain;
using Domain.Exceptions;
using ProtocolLibrary;
using ProtocolLibrary.FileHandler;
using ProtocolLibrary.FileHandler.Interfaces;

namespace GameStoreClient
{
    public class Runtime
    {
        private bool _exit;
        private readonly List<Game> _gamesLoaded = new List<Game>();
        private readonly IFileStreamHandler _fileStreamHandler = new FileStreamHandler();
        private readonly IFileHandler _fileHandler = new FileHandler();
        private TcpClient _client;
        private NetworkStream _networkStream;
        
        public async Task ExecuteAsync(TcpClient connectedClient)
        {
            _client = connectedClient;
            _networkStream = connectedClient.GetStream();
            
            Console.WriteLine("Welcome to the client system");
            Console.Write("Enter you username (in case it doesnt exists one will be created): ");
            var user = Console.ReadLine();
            try
            {
                await RequestAsync(user, CommandConstants.Login);
                var bufferResponse = await ResponseAsync(CommandConstants.Login);
                Console.WriteLine(Encoding.UTF8.GetString(bufferResponse));

                while (!_exit)
                {
                    Console.WriteLine("\n Options: ");
                    Console.WriteLine("list -> Visualize games list");
                    Console.WriteLine("publish -> Publish a game");
                    Console.WriteLine("publishedgames -> Visualize your published games list");
                    Console.WriteLine("exit -> Quit the program");
                    Console.Write("Type your option: ");
                    var option = Console.ReadLine();
                    if (option != null)
                    {
                        switch (option.ToLower())
                        {
                            case "list":
                                await ListGamesAsync();
                                break;
                            case "publish":
                                await PublishAsync();
                                break;
                            case "publishedgames":
                                await ListPublishedGamesAsync();
                                break;
                            case "exit":
                                _networkStream.Close();
                                _client.Close();
                                _exit = true;
                                break;
                            default:
                                Console.WriteLine("Invalid option");
                                break;
                        }
                    }
                }
            }
            catch (ServerDisconnected s)
            {
                _exit = true;
                Console.WriteLine(s.Message);
            }
            Console.WriteLine("Exiting Application");
        }

        private async Task ListPublishedGamesAsync()
        {
            await RequestAsync("", CommandConstants.ListPublishedGames);
            var bufferResponse = await ResponseAsync(CommandConstants.ListPublishedGames);
            var lengthString = Encoding.UTF8.GetString(bufferResponse);
            var length = Convert.ToInt32(lengthString);
            var gamesPublished = new List<Game>();
            Console.WriteLine("\n Published games list: \n");
            for (var i = 0; i < length; i++)
            {
                bufferResponse = await ResponseAsync(CommandConstants.ListPublishedGames);
                var split = (Encoding.UTF8.GetString(bufferResponse)).Split("*");
                var game = new Game(split[1], split[2], split[4], split[5])
                {
                    Id = Convert.ToInt32(split[0]),
                    Rating = Convert.ToInt32(split[3]),
                    Image = split[5]
                };
                gamesPublished.Add(game);
                Console.WriteLine($"{game.Id}. {game.Title} - {game.Genre} - {game.Rating}\n");
            }
            
            var main = false;
            while (!main)
            {
                Console.WriteLine("\n Options:");
                Console.WriteLine("modify -> Modify an existing game");
                Console.WriteLine("modifyimage -> Modify an existing game image");
                Console.WriteLine("delete -> Delete a game");
                Console.WriteLine("main <- Go to main menu");
                Console.Write("Option: ");
                var option = Console.ReadLine();
                if (option != null)
                    switch (option.ToLower())
                    {
                        case "modify":
                            await ModifyGameAsync(gamesPublished);
                            break;
                        case "modifyimage":
                            await ModifyImageAsync(gamesPublished);
                            break;
                        case "delete":
                            await DeleteGameAsync(gamesPublished);
                            break;
                        case "main":
                            main = true;
                            break;
                        default:
                            Console.WriteLine("Invalid option");
                            break;
                    }
            }
        }

        private async Task ModifyImageAsync(List<Game> gamesPublished)
        {
            var correctId = false;
            while (!correctId)
            {
                Console.Write("Insert the id of the game to modify its image: ");
                var gameId = Console.ReadLine();
                var game = gamesPublished.Find(e => e.Id.Equals(Convert.ToInt32(gameId)));
                if (game == null)
                {
                    Console.WriteLine("Did not found any game with the selected id");
                }
                else
                {
                    Console.WriteLine("Insert the path of the new file:");
                    var path = String.Empty;
                    IFileHandler fileHandler = new FileHandler();
                    while(path != null && path.Equals(string.Empty) && !fileHandler.FileExists(path))
                    {
                        path = Console.ReadLine();
                    }

                    var info = gameId + "*" + path;
                    try
                    {
                        var fileName = _fileHandler.GetFileName(path);
                        var fileSize = _fileHandler.GetFileSize(path);
                        await RequestAsync(info, CommandConstants.ModifyImage);
                        await SendFileAsync(path, fileName, fileSize);
                        Console.WriteLine("Sent");
                        var bufferResponse = await ResponseAsync(CommandConstants.ModifyImage);
                        Console.WriteLine(Encoding.UTF8.GetString(bufferResponse));
                        correctId = true;
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("La modificacion ha fallado.");
                    }
                }
            }
        }

        private async Task PublishAsync()
        {
            Console.WriteLine("\n Publish a game");
            var game = "";
            var attributes = new List<string>
            {
                "Title", "Genre", "Sinopsis", "Path of the image"
            };
            foreach (var attribute in attributes)
            {
                Console.Write($"\n  Insert the {attribute}:");
                var insert = Console.ReadLine();
                game += insert + "*";
            }
            
            var splittedGame = game.Split("*");
            var path = splittedGame[3];
            IFileHandler fileHandler = new FileHandler();
            while(path != null && path.Equals(string.Empty) && !fileHandler.FileExists(path))
            {
                path = Console.ReadLine();
            }

            try
            {
                var fileName = _fileHandler.GetFileName(path);
                var fileSize = _fileHandler.GetFileSize(path);
                await RequestAsync(game,CommandConstants.Publish);
                await SendFileAsync(path,fileName,fileSize);
                Console.WriteLine("Sent");

                var bufferResponse = await ResponseAsync(CommandConstants.Publish);
                Console.WriteLine(Encoding.UTF8.GetString(bufferResponse));
            }
            catch (Exception)
            {
                Console.WriteLine("La publicacion ha fallado.");
            }
        }

        
        private async Task ModifyGameAsync(List<Game> gamesPublished)
        {
            var correctId = false;
            while (!correctId)
            {
                Console.Write("Insert the id of the game to modify: ");
                var gameId = Console.ReadLine();
                var game = gamesPublished.Find(e => e.Id.Equals(Convert.ToInt32(gameId)));
                if (game == null)
                {
                    Console.WriteLine("Did not found any game with the selected id");
                }
                else
                {
                    var gameModified = gameId+"*";
                    var attributes = new List<string>
                    {
                        "Title", "Genre", "Sinopsis"
                    };
                    foreach (var attribute in attributes)
                    {
                        Console.Write($"\n Insert the new {attribute} (in case of not modifying it, leave empty):");
                        var insert = Console.ReadLine();
                        if (insert is "")
                        {
                            gameModified += "-" + "*";
                        }
                        else
                        {
                            gameModified += insert + "*";
                        }
                    }
                    await RequestAsync(gameModified,CommandConstants.ModifyGame);
                    var bufferResponse = await ResponseAsync(CommandConstants.ModifyGame);
                    Console.WriteLine(Encoding.UTF8.GetString(bufferResponse));

                    correctId = true;
                }
            }
        }

        private async Task DeleteGameAsync(List<Game> gamesPublished)
        {
            var correctId = false;
            while (!correctId)
            {
                Console.Write("Insert the id of the game to delete: ");
                var gameId = Console.ReadLine();
                var game = gamesPublished.Find(e => e.Id.Equals(Convert.ToInt32(gameId)));
                if (game == null)
                {
                    Console.WriteLine("Did not found any game with the selected id");
                }
                else
                {
                    await RequestAsync(gameId, CommandConstants.DeleteGame);
                    var bufferResponse = await ResponseAsync(CommandConstants.DeleteGame);
            
                    Console.WriteLine(Encoding.UTF8.GetString(bufferResponse));
                    correctId = true;
                }
            }
        }

        private async Task ListGamesAsync()
        {
            await RequestAsync("", CommandConstants.ListGames);
            var bufferResponse = await ResponseAsync(CommandConstants.ListGames);
            var lengthString = Encoding.UTF8.GetString(bufferResponse);
            var length = Convert.ToInt32(lengthString);
            _gamesLoaded.Clear();
            Console.WriteLine("\n Games list: \n");
            for (var i = 0; i < length; i++)
            {
                bufferResponse = await ResponseAsync(CommandConstants.ListGames);
                var split = (Encoding.UTF8.GetString(bufferResponse)).Split("*");
                var game = new Game(split[1], split[2], split[4], split[5])
                {
                    Id = Convert.ToInt32(split[0]),
                    Rating = Convert.ToInt32(split[3]),
                    Image = split[5]
                };
                Console.WriteLine($"{game.Id}. {game.Title} - {game.Genre} - {game.Rating}\n");
                _gamesLoaded.Add(game);
            }
            
            var main = false;
            while (!main)
            {
                Console.WriteLine("\n Options:");
                Console.WriteLine("detail -> Get details of a game");
                Console.WriteLine("purchase -> Purchase a game");
                Console.WriteLine("purchasedgames -> List the games already purchased by the user");
                Console.WriteLine("search -> Search a game by its Title, Category");
                Console.WriteLine("filterrating -> Filter games by a minimum rating");
                Console.WriteLine("reviews -> Get reviews of a game");
                Console.WriteLine("rate -> Rate and comment a game");
                Console.WriteLine("download -> Download the image of a game");
                Console.WriteLine("main <- Go to main menu");
                Console.Write("Option: ");
                var option = Console.ReadLine();
                switch (option)
                {
                    case "detail":
                        await DetailGameAsync();
                        break;
                    case "purchase":
                        await PurchaseAsync();
                        break;
                    case "purchasedgames":
                        await GetPurchasedGamesAsync();
                        break;
                    case "search":
                        await SearchAsync();
                        break;
                    case "filterrating":
                        await SearchRatingAsync();
                        break;
                    case "reviews":
                        await GetReviewsAsync();
                        break;
                    case "rate":
                        await RateAsync();
                        break;
                    case "download":
                        await DownloadAsync();
                        break;
                    case "main":
                        main = true;
                        break;
                    default:
                        Console.WriteLine("Invalid option");
                        break;
                }
            }
        }

        private async Task GetPurchasedGamesAsync()
        {
            await RequestAsync("", CommandConstants.ListPurchasedGames);
            var bufferResponse = await ResponseAsync(CommandConstants.ListPurchasedGames);
            var lengthString = Encoding.UTF8.GetString(bufferResponse);
            var length = Convert.ToInt32(lengthString);
            var gamesPurchased = new List<Game>();
            Console.WriteLine("\n Purchased games list: \n");
            for (var i = 0; i < length; i++)
            {
                bufferResponse = await ResponseAsync(CommandConstants.ListPurchasedGames);
                var split = (Encoding.UTF8.GetString(bufferResponse)).Split("*");
                var game = new Game(split[1], split[2], split[4], split[5])
                {
                    Id = Convert.ToInt32(split[0]),
                    Rating = Convert.ToInt32(split[3])
                };
                gamesPurchased.Add(game);
                Console.WriteLine($"{game.Id}: {game.Title} - {game.Genre} - {game.Rating}\n");
            }
        }

        private async Task SendParametersAsync(string parameter, int command)
        {
            await RequestAsync(parameter, command);
            var bufferResponse = await ResponseAsync(command);
            var length = Convert.ToInt32(Encoding.UTF8.GetString(bufferResponse));

            _gamesLoaded.Clear();
            Console.WriteLine("\n Search result: \n");
            if (length == 0)
            {
                Console.WriteLine("0 games found with the indicated parameters");
            }
            else
            {
                for (var i = 0; i < length; i++)
                {
                    bufferResponse = await ResponseAsync(command);
                    var split = (Encoding.UTF8.GetString(bufferResponse)).Split("*");
                    var game = new Game(split[1], split[2], split[4], split[5])
                    {
                        Id = Convert.ToInt32(split[0]),
                        Rating = Convert.ToInt32(split[3])
                    };
                    _gamesLoaded.Add(game);
                    Console.WriteLine($"{game.Id}. {game.Title} - {game.Genre} - {game.Rating}\n");
                }
            }
        }

        private async Task SearchRatingAsync()
        {
            Console.WriteLine("Insert a minimum rating to filter games: ");
            var minimumRating = Console.ReadLine();
            await SendParametersAsync(minimumRating, CommandConstants.FilterRating);
        }

        private async Task SearchAsync()
        {
            Console.WriteLine("Insert some keywords to search a game: ");
            var keywords = Console.ReadLine();
            await SendParametersAsync(keywords,CommandConstants.Search);
        }

        private async Task DownloadAsync()
        {
            var correctId = false;
            while (!correctId)
            {
                Console.Write("Insert the id of the game to download its image: ");
                var gameId = Console.ReadLine();
                var game = _gamesLoaded.Find(e => e.Id.Equals(Convert.ToInt32(gameId)));
                if (game == null)
                {
                    Console.WriteLine("Did not found any game with the selected id");
                }
                else
                {
                    await RequestAsync(game.Id.ToString(),CommandConstants.Download);
                    var bufferResponse = await ResponseAsync(CommandConstants.Download);
                    Console.WriteLine(Encoding.UTF8.GetString(bufferResponse));
                    
                    await ReceiveFileAsync();
                    Console.WriteLine("File received");
                    correctId = true;
                }
            }
        }

        private async Task RateAsync()
        {
            Console.WriteLine("\n Rate and comment a game");
            var correctId = false;
            while (!correctId)
            {
                Console.Write("Insert the id of the game to rate and comment: ");
                var gameId = Console.ReadLine();
                var game = _gamesLoaded.Find(e => e.Id.Equals(Convert.ToInt32(gameId)));
                if (game == null)
                {
                    Console.WriteLine("Did not found any game with the selected id");
                }
                else
                {
                    var review = gameId + "*";
                    Console.WriteLine("Type the rating in a 1 to 5 range");
                    var correctRating = false;
                    while (!correctRating)
                    {
                        var insert = Console.ReadLine();
                        int rating = Convert.ToInt32(insert);
                        if (rating < 1 || rating > 5)
                        {
                            Console.WriteLine("Incorrect rating, try again. It must be in the 1 to 5 range");
                        }
                        else
                        {
                            review += insert + "*";
                            correctRating = true;
                        }
                    }
                    Console.WriteLine("Type your comment");
                    var comment = Console.ReadLine();
                    review += comment;
                    await RequestAsync(review,CommandConstants.Rate);
                    var bufferResponse = await ResponseAsync(CommandConstants.Rate);
                    Console.WriteLine(Encoding.UTF8.GetString(bufferResponse));
                    correctId = true;
                }
            }
        }

        private async Task GetReviewsAsync()
        {
            var correctId = false;
            while (!correctId)
            {
                Console.Write("Insert the id of the game to get its reviews: ");
                var gameId = Console.ReadLine();
                var game = _gamesLoaded.Find(e => e.Id.Equals(Convert.ToInt32(gameId)));
                if (game == null)
                {
                    Console.WriteLine("Did not found any game with the selected id");
                }
                else
                {
                    await RequestAsync(gameId, CommandConstants.GetReviews);
                    var bufferResponse = await ResponseAsync(CommandConstants.GetReviews);
                    var lengthString = Encoding.UTF8.GetString(bufferResponse);
                    var length = Convert.ToInt32(lengthString);
                    Console.WriteLine("\n Game reviews: \n");
                    if (length == 0)
                    {
                        Console.WriteLine("The game doesnt have any reviews");
                    }
                    else
                    {
                        for (var i = 0; i < length; i++)
                        {
                            bufferResponse = await ResponseAsync(CommandConstants.GetReviews);
                            var splittedReview = (Encoding.UTF8.GetString(bufferResponse)).Split("*");
                            var rating = splittedReview[0];
                            var comment = splittedReview[1];
                            Console.WriteLine($"{i+1}: Rating: {rating}");
                            Console.WriteLine($"{comment}");
                        }
                    }
                    correctId = true;
                }
            }
        }

        private async Task PurchaseAsync()
        {
            var correctId = false;
            while (!correctId)
            {
                Console.Write("Insert the id of the game to purchase: ");
                var gameId = Console.ReadLine();
                var game = _gamesLoaded.Find(e => e.Id.Equals(Convert.ToInt32(gameId)));
                if (game == null)
                {
                    Console.WriteLine("Did not found any game with the selected id");
                }
                else
                {
                    await RequestAsync(gameId, CommandConstants.Purchase);
                    var bufferResponse = await ResponseAsync(CommandConstants.Purchase);
            
                    Console.WriteLine(Encoding.UTF8.GetString(bufferResponse));
                    correctId = true;
                }
            }
        }

        private async Task DetailGameAsync()
        {
            var correctId = false;
            while (!correctId)
            {
                Console.Write("Insert the id of the game to select: ");
                var id = Console.ReadLine();
                var game = _gamesLoaded.Find(e => e.Id.Equals(Convert.ToInt32(id)));
                if (game == null)
                {
                    Console.WriteLine("Did not found any game with the selected id");
                }
                else
                {
                    await RequestAsync(id,CommandConstants.DetailGame);
                    var bufferResponse = await ResponseAsync(CommandConstants.DetailGame);
                    var responseString = Encoding.UTF8.GetString(bufferResponse);
                    if (responseString.Contains("*"))
                    {
                        var split = (responseString).Split("*");

                        Console.WriteLine($" --- {split[1]} --- ");
                        Console.WriteLine($"{split[2]} *{split[3]}");
                        Console.WriteLine(split[4]);
                        Console.WriteLine($"Image file name: {split[5]}");
                    }
                    else
                    {
                        Console.WriteLine(responseString);
                    }

                    correctId = true;
                }
            }
        }

        private async Task<byte[]> ResponseAsync(int command)
        {
            var bufferResponse = new byte[] { };
            var headerLength = HeaderConstants.Response.Length + HeaderConstants.CommandLength +
                               HeaderConstants.DataLength;
            var buffer = new byte[headerLength];
            try
            {
                await ReceiveDataAsync(headerLength, buffer);
                var header = new Header();
                header.DecodeData(buffer);
                if (header.ICommand.Equals(command))
                {
                    bufferResponse = new byte[header.IDataLength];
                    await ReceiveDataAsync(header.IDataLength, bufferResponse);
                }
                else
                {
                    Console.WriteLine(header.ICommand + " " + command);
                }

            }
            catch (IOException)
            {
                throw new ServerDisconnected();
            }
            catch (Exception e)
            {
                Console.WriteLine($"---- -> Message {e.Message}..");
            }
            return bufferResponse;
        }

        private async Task RequestAsync(string message, int command)
        {
            var header = new Header(HeaderConstants.Request, command, message.Length);
            var data = header.GetRequest();
            var bytesMessage = Encoding.UTF8.GetBytes(message);
            try
            {
                await _networkStream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                await _networkStream.WriteAsync(bytesMessage, 0, bytesMessage.Length).ConfigureAwait(false);
            }
            catch (IOException)
            {
                throw new ServerDisconnected();
            }
        }
        
        private async Task ReceiveDataAsync(int length, byte[] buffer)
        {
            var iRecv = 0;
            while (iRecv < length)
            {
                var received = await _networkStream
                    .ReadAsync(buffer, iRecv, length - iRecv)
                    .ConfigureAwait(false);
                iRecv += received;
            }
        }
        
        private async Task SendFileAsync(string path, string fileName, long fileSize)
        {
            var header = new Header().Create(fileName, fileSize);
            await _networkStream.WriteAsync(header, 0, header.Length);
            
            var fileNameToBytes = Encoding.UTF8.GetBytes(fileName);
            await _networkStream.WriteAsync(fileNameToBytes, 0, fileNameToBytes.Length);
            
            var parts = Header.GetParts(fileSize);
            Console.WriteLine("Will Send {0} parts",parts);
            long offset = 0;
            long currentPart = 1;

            while (fileSize > offset)
            {
                byte[] data;
                if (currentPart == parts)
                {
                    var lastPartSize = (int)(fileSize - offset);
                    data = _fileStreamHandler.Read(path, offset, lastPartSize);
                    offset += lastPartSize;
                }
                else
                {
                    data = _fileStreamHandler.Read(path, offset, HeaderConstants.MaxPacketSize);
                    offset += HeaderConstants.MaxPacketSize;
                }
                await _networkStream.WriteAsync(data, 0, data.Length);
                currentPart++;
            }
        }

        private async Task ReceiveFileAsync()
        {
            var fileHeader = new byte[Header.GetLength()];
            await ReceiveDataAsync(Header.GetLength(), fileHeader);
            var fileNameSize = BitConverter.ToInt32(fileHeader, 0);
            var fileSize = BitConverter.ToInt64(fileHeader, HeaderConstants.FixedFileNameLength);
            
            var bufferName = new byte[fileNameSize];
            await ReceiveDataAsync(fileNameSize, bufferName);
            var fileName = Encoding.UTF8.GetString(bufferName);
            
            var parts = Header.GetParts(fileSize);
            long offset = 0;
            long currentPart = 1;

            Console.WriteLine(
                $"Will receive file {fileName} with size {fileSize} that will be received in {parts} segments");
            while (fileSize > offset)
            {
                byte[] data;
                if (currentPart == parts)
                {
                    var lastPartSize = (int) (fileSize - offset);
                    Console.WriteLine($"Will receive segment number {currentPart} with size {lastPartSize}");
                    data = new byte[lastPartSize];
                    await ReceiveDataAsync(lastPartSize, data);
                    offset += lastPartSize;
                }
                else
                {
                    Console.WriteLine(
                        $"Will receive segment number {currentPart} with size {HeaderConstants.MaxPacketSize}");
                    data = new byte[HeaderConstants.MaxPacketSize];
                    await ReceiveDataAsync(HeaderConstants.MaxPacketSize, data);
                    offset += HeaderConstants.MaxPacketSize;
                }

                _fileStreamHandler.Write(fileName, data);
                currentPart++;
            }
        }

    }
}