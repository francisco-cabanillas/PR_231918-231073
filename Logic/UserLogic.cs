﻿using System;
using LogicInterface;
using Microsoft.Extensions.DependencyInjection;
using RepositoryInterface;

namespace Logic
{
    public class UserLogic : IUserLogic
    {
        private IUserLogic userRepository;

        public UserLogic(IServiceProvider serviceProvider)
        {
            userRepository = serviceProvider.GetService<IUserLogic>();
            
        }
    }
}