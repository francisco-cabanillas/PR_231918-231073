﻿
namespace Domain
{
    public class Game
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Genre { get; set; }
        public int Rating { get; set; }
        public string Sinopsis { get; set; }
        public string Image { get; set; }
        public User Creator { get; set; }

        public Game(string title, string genre, string sinopsis, string path)
        {
            this.Title = title;
            this.Genre = genre;
            this.Sinopsis = sinopsis;
            this.Rating = 0;
            this.Image = path;
        }

        public Game(string title, string genre, string sinopsis)
        {
            this.Title = title;
            this.Genre = genre;
            this.Sinopsis = sinopsis;
            this.Rating = 0;
        }
    }
}
