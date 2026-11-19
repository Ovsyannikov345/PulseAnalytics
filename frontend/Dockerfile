FROM node:26-alpine AS build
WORKDIR /src

COPY package.json package-lock.json ./

RUN npm ci --ignore-scripts --no-audit --no-fund

COPY . .

RUN npm run build

FROM nginxinc/nginx-unprivileged:1.30-alpine AS final
ENV API_GATEWAY_URL=http://api-gateway:8080 \
    NOTIFICATION_SERVICE_URL=http://notification-service:8080 \
    # Docker's embedded DNS
    NGINX_RESOLVER=127.0.0.11 \
    NGINX_ENVSUBST_FILTER="^(API_GATEWAY_URL|NOTIFICATION_SERVICE_URL|NGINX_RESOLVER)$"

COPY nginx/security-headers.conf /etc/nginx/security-headers.conf
COPY nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /src/dist /usr/share/nginx/html

EXPOSE 8080
