using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ConduitLLM.Configuration;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Client for interacting with RabbitMQ Management API.
    /// </summary>
    public class RabbitMQManagementClient : IRabbitMQManagementClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<RabbitMQManagementClient> _logger;
        private readonly string _baseUrl;
        private readonly JsonSerializerOptions _jsonOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="RabbitMQManagementClient"/> class.
        /// </summary>
        /// <param name="httpClient">HTTP client.</param>
        /// <param name="configuration">Configuration.</param>
        /// <param name="logger">Logger.</param>
        public RabbitMQManagementClient(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<RabbitMQManagementClient> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Use the same configuration section as the rest of the application
            var rabbitMqConfig = configuration.GetSection("ConduitLLM:RabbitMQ").Get<RabbitMqConfiguration>() 
                ?? new RabbitMqConfiguration();

            _baseUrl = $"http://{rabbitMqConfig.Host}:{rabbitMqConfig.ManagementPort}/api";

            // Set up basic authentication
            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{rabbitMqConfig.Username}:{rabbitMqConfig.Password}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<QueueInfo>> GetQueuesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/queues", cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get queues from RabbitMQ Management API: {StatusCode}", response.StatusCode);
                    return Enumerable.Empty<QueueInfo>();
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var queues = JsonSerializer.Deserialize<List<JsonElement>>(json, _jsonOptions);

                if (queues == null)
                {
                    return Enumerable.Empty<QueueInfo>();
                }

                return queues.Select(q => ParseQueueInfo(q)).Where(q => q != null)!;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting queues from RabbitMQ Management API");
                return Enumerable.Empty<QueueInfo>();
            }
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<RabbitMQMessage>> GetMessagesAsync(
            string queueName, 
            int count,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var requestBody = new
                {
                    count = count,
                    ackmode = "ack_requeue_true",
                    encoding = "auto"
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(requestBody, _jsonOptions),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync(
                    $"{_baseUrl}/queues/%2F/{Uri.EscapeDataString(queueName)}/get",
                    content,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get messages from queue {QueueName}: {StatusCode}", 
                        queueName, response.StatusCode);
                    return Enumerable.Empty<RabbitMQMessage>();
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var messages = JsonSerializer.Deserialize<List<JsonElement>>(json, _jsonOptions);

                if (messages == null)
                {
                    return Enumerable.Empty<RabbitMQMessage>();
                }

                return messages.Select(m => ParseMessage(m)).Where(m => m != null)!;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting messages from queue {QueueName}", queueName);
                return Enumerable.Empty<RabbitMQMessage>();
            }
        }

        /// <inheritdoc/>
        public async Task<RabbitMQMessage?> GetMessageAsync(
            string queueName, 
            string messageId,
            CancellationToken cancellationToken = default)
        {
            // RabbitMQ Management API doesn't support getting a specific message by ID
            // We need to get messages and find the one with matching ID
            var messages = await GetMessagesAsync(queueName, 100, cancellationToken);
            return messages.FirstOrDefault(m => m.Properties.MessageId == messageId);
        }

        private QueueInfo? ParseQueueInfo(JsonElement queueData)
        {
            try
            {
                var queueInfo = new QueueInfo
                {
                    Name = queueData.GetProperty("name").GetString() ?? string.Empty,
                    Messages = queueData.TryGetProperty("messages", out JsonElement messages) 
                        ? messages.GetInt64() : 0,
                    MessageBytes = queueData.TryGetProperty("message_bytes", out JsonElement bytes) 
                        ? bytes.GetInt64() : 0,
                    Consumers = queueData.TryGetProperty("consumers", out JsonElement consumers) 
                        ? consumers.GetInt32() : 0,
                    State = queueData.TryGetProperty("state", out JsonElement state) 
                        ? state.GetString() ?? "unknown" : "unknown"
                };

                if (queueData.TryGetProperty("message_stats", out JsonElement stats))
                {
                    queueInfo = queueInfo with
                    {
                        MessageStats = new QueueStatistics
                        {
                            PublishRate = stats.TryGetProperty("publish_details", out JsonElement pubDetails) &&
                                         pubDetails.TryGetProperty("rate", out JsonElement pubRate)
                                ? pubRate.GetDouble() : 0,
                            DeliverRate = stats.TryGetProperty("deliver_get_details", out JsonElement delDetails) &&
                                         delDetails.TryGetProperty("rate", out JsonElement delRate)
                                ? delRate.GetDouble() : 0,
                            AckRate = stats.TryGetProperty("ack_details", out JsonElement ackDetails) &&
                                     ackDetails.TryGetProperty("rate", out JsonElement ackRate)
                                ? ackRate.GetDouble() : 0
                        }
                    };
                }

                return queueInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing queue info");
                return null;
            }
        }

        private RabbitMQMessage? ParseMessage(JsonElement messageData)
        {
            try
            {
                var message = new RabbitMQMessage
                {
                    Payload = messageData.GetProperty("payload").GetString() ?? string.Empty,
                    PayloadEncoding = messageData.TryGetProperty("payload_encoding", out JsonElement encoding)
                        ? encoding.GetString() ?? "string" : "string",
                    Redelivered = messageData.TryGetProperty("redelivered", out JsonElement redelivered) && redelivered.GetBoolean(),
                    Exchange = messageData.TryGetProperty("exchange", out JsonElement exchange)
                        ? exchange.GetString() ?? string.Empty : string.Empty,
                    RoutingKey = messageData.TryGetProperty("routing_key", out JsonElement routingKey)
                        ? routingKey.GetString() ?? string.Empty : string.Empty,
                    PayloadBytes = messageData.TryGetProperty("payload_bytes", out JsonElement payloadBytes)
                        ? payloadBytes.GetInt32() : 0
                };

                if (messageData.TryGetProperty("properties", out JsonElement properties))
                {
                    var headers = new Dictionary<string, object>();
                    if (properties.TryGetProperty("headers", out JsonElement headersElement))
                    {
                        foreach (var header in headersElement.EnumerateObject())
                        {
                            headers[header.Name] = header.Value.ToString();
                        }
                    }

                    message = message with
                    {
                        Properties = new RabbitMQMessageProperties
                        {
                            MessageId = properties.TryGetProperty("message_id", out JsonElement msgId)
                                ? msgId.GetString() : null,
                            CorrelationId = properties.TryGetProperty("correlation_id", out JsonElement corrId)
                                ? corrId.GetString() : null,
                            ContentType = properties.TryGetProperty("content_type", out JsonElement contentType)
                                ? contentType.GetString() : null,
                            Type = properties.TryGetProperty("type", out JsonElement type)
                                ? type.GetString() : null,
                            Timestamp = properties.TryGetProperty("timestamp", out JsonElement timestamp)
                                ? timestamp.GetInt64() : null,
                            Headers = headers
                        }
                    };
                }

                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing message");
                return null;
            }
        }
    }
}