# Build da SPA (React + Vite).
FROM node:20-alpine AS build
WORKDIR /app

# O Vite substitui import.meta.env.* no momento do build, então o valor precisa
# estar disponível AQUI, em tempo de build. Sem estes ARGs, o container cairia
# sempre no padrão do código (dados de exemplo) e a integração com a API
# pareceria quebrada sem erro nenhum.
ARG VITE_API_BASE_URL=http://localhost:5000
ARG VITE_USE_SAMPLE_DATA=false
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
ENV VITE_USE_SAMPLE_DATA=$VITE_USE_SAMPLE_DATA

COPY package.json ./
RUN npm install
COPY . ./
RUN npm run build

# Servidor estático de produção (portátil: funciona no Docker Desktop e no WSL 2).
FROM nginx:1.27-alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]