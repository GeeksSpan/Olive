﻿using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Olive.RabbitMQ
{
    class Subscriber
    {
        public Func<string, Task> Handler { get; }
        EventBusQueue Queue;
        Thread PollingThread;

        public Subscriber(EventBusQueue queue, Func<string, Task> handler)
        {
            Handler = handler;
            Queue = queue;
        }

        public void Start()
        {
            (PollingThread = new Thread(async () => await KeepPolling())).Start();
        }

        public Task PullAll() => KeepPolling(PullStrategy.UntilEmpty, waitTimeSeconds: 0);


        void Poll(int waitTimeSeconds)
        {
            var consumer = new EventingBasicConsumer(Queue.Client);

            consumer.Received += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    Log.For(this)
                        .Info($"RabbitMQ recieved message: Queue " + Queue.QueueUrl);
                    await Handler(message);
                    Queue.Client.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    var exception = new Exception("Failed to run queue event handler " +
                        Handler.Method.DeclaringType.GetProgrammingName() + "." +
                        Handler.Method.Name +
                        "message: " + ea.DeliveryTag.ToString()?.ToJsonText(), ex);

                    if (Queue.IsFifo)
                        throw exception;
                    else
                        Log.For<Subscriber>().Error(exception);
                }
            };
            Queue.Client.ExchangeDeclare(exchange: Queue.QueueUrl, type: ExchangeType.Fanout, durable: true);
            Queue.Client.QueueDeclare(Queue.QueueUrl, true, false, false, null);
            Queue.Client.QueueBind(queue: Queue.QueueUrl,
                  exchange: Queue.QueueUrl,
                  routingKey: Queue.QueueUrl);
            Queue.Client.BasicConsume(queue: Queue.QueueUrl,
                                autoAck: false,
                                consumer: consumer);
        }

        async Task KeepPolling(PullStrategy strategy = PullStrategy.KeepPulling, int waitTimeSeconds = 10)
        {
            await Task.Run(() => { });
            Poll(waitTimeSeconds);
            //var queueIsEmpty = false;
            //do
            //{
            //try { queueIsEmpty = !await Poll(waitTimeSeconds); }
            //catch (Exception exception) { Log.For<Subscriber>().Error(exception); }
            //}
            //while (strategy == PullStrategy.KeepPulling || !queueIsEmpty);
        }
    }
}