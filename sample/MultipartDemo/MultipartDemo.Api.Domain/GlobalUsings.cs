global using System;
global using System.Collections.Concurrent;
global using System.Collections.Generic;
global using System.Runtime.CompilerServices;
global using System.Threading;
global using System.Threading.Tasks;

global using FluentValidation;

global using Microsoft.Extensions.DependencyInjection;
global using MultipartDemo.Generated.Accounts.Handlers;
global using MultipartDemo.Generated.Accounts.Models;
global using MultipartDemo.Generated.Accounts.Parameters;
global using MultipartDemo.Generated.Accounts.Results;
global using MultipartDemo.Generated.Files.Handlers;
global using MultipartDemo.Generated.Files.Parameters;
global using MultipartDemo.Generated.Files.Results;
global using MultipartDemo.Generated.Notifications.Handlers;
global using MultipartDemo.Generated.Notifications.Models;
global using MultipartDemo.Generated.Notifications.Parameters;
global using MultipartDemo.Generated.Notifications.Results;
global using MultipartDemo.Generated.Tasks.Handlers;
global using MultipartDemo.Generated.Tasks.Parameters;
global using MultipartDemo.Generated.Tasks.Results;
global using MultipartDemo.Generated.Testings.Handlers;
global using MultipartDemo.Generated.Testings.Parameters;
global using MultipartDemo.Generated.Testings.Results;
global using MultipartDemo.Generated.Users.Handlers;
global using MultipartDemo.Generated.Users.Models;
global using MultipartDemo.Generated.Users.Parameters;
global using MultipartDemo.Generated.Users.Results;

// Alias for Task model to avoid conflict with System.Threading.Tasks.Task
global using TaskModel = MultipartDemo.Generated.Tasks.Models.Task;