﻿using System;
using System.Collections.Generic;
using System.Text;
using Domain;
using Microsoft.EntityFrameworkCore;
using RepositoryInterface;

namespace Repository
{
    public class ReviewRepository : IReviewRepository
    {
        private readonly List<Review> reviews;

        public ReviewRepository()
        {
            this.reviews = new List<Review>();
        }

        public void Add(Review review)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Review> GetAll()
        {
            throw new NotImplementedException();
        }
    }
}
