﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using AwesomeBot.Common;
using Victoria;
using Infrastructure;

namespace AwesomeBot.Services
{
    public class CommandHandler
    {
        public static IServiceProvider _provider;
        public static DiscordSocketClient _discord;
        public static CommandService _command;
        public static IConfigurationRoot _config;
        private readonly LavaNode _lavaNode;
        public static List<Mute> Mutes = new List<Mute>();
        private readonly Servers _servers;


        public CommandHandler(DiscordSocketClient discord, CommandService commands, IConfigurationRoot config, IServiceProvider provider, LavaNode lavaNode, Servers servers)
        {
            _provider = provider;
            _discord = discord;
            _command = commands;
            _config = config;
            _lavaNode = lavaNode;
            _servers = servers;

            var newTask = new Task(async () => await MuteHandler());
            newTask.Start();
            _discord.Ready += OnReady;
            _discord.Ready += onReadyAsync;
            _discord.MessageReceived += _discord_MessageReceived;
        }
        private async Task MuteHandler()
        {
            List<Mute> Remove = new List<Mute>();
            foreach(var mute in Mutes)
            {
                if (DateTime.Now < mute.End)
                    continue;
                var guild = _discord.GetGuild(mute.Guild.Id);
                if(guild.GetRole(mute.Role.Id) == null)
                {
                    Remove.Add(mute);
                    continue;
                }
                
                if (guild.GetUser(mute.User.Id) == null)
                {
                    Remove.Add(mute);
                    continue;
                }

                var user = guild.GetUser(mute.User.Id);

                if (guild.GetRole(mute.Role.Id).Position > guild.CurrentUser.Hierarchy)
                {
                    Remove.Add(mute);
                    continue;
                }
                await mute.User.RemoveRoleAsync(mute.Role);
                Remove.Add(mute);
            }
            Mutes = Mutes.Except(Remove).ToList();

            await Task.Delay(1 * 60 * 1000);
            await MuteHandler();
        }

        private async Task _discord_MessageReceived(SocketMessage arg)
        {
            
            var msg = arg as SocketUserMessage;
            var prefix = await _servers.GetGuildPrefix((msg.Channel as SocketGuildChannel).Guild.Id) ?? "!";

            if (msg != null)
            {
                if (msg.Author.IsBot)
                {
                    return;
                }

                var context = new SocketCommandContext(_discord, msg);
                int pos = 0;
                if (msg.HasStringPrefix(prefix, ref pos) || msg.HasMentionPrefix(_discord.CurrentUser, ref pos))
                {
                    var result = await _command.ExecuteAsync(context, pos, _provider);
                    if (!result.IsSuccess)
                    {
                        var reason = result.Error;
                        string errorMessage = $"The following error occurred: \n {reason}";
                        await context.Channel.SendMessageAsync(errorMessage);
                        Console.WriteLine(errorMessage);


                    }
                    else
                    {
                        Console.WriteLine(result);
                    }
                }
            }
        }
        private async Task onReadyAsync()
        {
            if(!_lavaNode.IsConnected)
            {
                await _lavaNode.ConnectAsync();
            }
        }
        private Task OnReady()
        {

            Console.WriteLine($"Connected as {_discord.CurrentUser.Username}#{_discord.CurrentUser.Discriminator}");
            return Task.CompletedTask;
        }
    }
}
