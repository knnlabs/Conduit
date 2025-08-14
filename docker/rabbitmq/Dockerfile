# =============================================================================
# RabbitMQ with Delayed Message Exchange Plugin
# =============================================================================
# Custom RabbitMQ image with essential plugins for Conduit message processing
# =============================================================================

FROM rabbitmq:3-management-alpine

# Plugin version - update as needed
ARG DELAYED_MESSAGE_PLUGIN_VERSION=3.13.0

# Install the delayed message exchange plugin with proper error handling
RUN set -eux; \
    apk add --no-cache --virtual .build-deps wget ca-certificates; \
    cd /opt/rabbitmq/plugins; \
    wget -O rabbitmq_delayed_message_exchange-${DELAYED_MESSAGE_PLUGIN_VERSION}.ez \
        "https://github.com/rabbitmq/rabbitmq-delayed-message-exchange/releases/download/v${DELAYED_MESSAGE_PLUGIN_VERSION}/rabbitmq_delayed_message_exchange-${DELAYED_MESSAGE_PLUGIN_VERSION}.ez" || \
        (echo "Failed to download delayed message exchange plugin" && exit 1); \
    rabbitmq-plugins enable --offline rabbitmq_delayed_message_exchange; \
    rabbitmq-plugins enable --offline rabbitmq_prometheus; \
    rabbitmq-plugins enable --offline rabbitmq_shovel; \
    rabbitmq-plugins enable --offline rabbitmq_shovel_management; \
    apk del .build-deps; \
    rm -rf /var/cache/apk/*

# Configure RabbitMQ for production use
ENV RABBITMQ_CONFIG_FILE=/etc/rabbitmq/rabbitmq

# Copy custom configuration
COPY rabbitmq.conf /etc/rabbitmq/rabbitmq.conf
# COPY definitions.json /etc/rabbitmq/definitions.json

# Labels for documentation
LABEL org.opencontainers.image.title="Conduit RabbitMQ" \
      org.opencontainers.image.description="RabbitMQ with delayed message exchange for Conduit" \
      org.opencontainers.image.vendor="KNN Labs" \
      org.opencontainers.image.source="https://github.com/knnlabs/Conduit" \
      rabbitmq.version="3.13" \
      rabbitmq.delayed-message-plugin.version="${DELAYED_MESSAGE_PLUGIN_VERSION}"