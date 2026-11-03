# ---------- Stage 1: build the Angular app ----------
FROM node:20.17.0-alpine AS build
WORKDIR /app

# Install packages first (cached when only code changes)
COPY package*.json ./
RUN npm ci

# Copy the code and build the production version
COPY . .
RUN npm run build

# ---------- Stage 2: serve it with nginx ----------
FROM nginx:alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/shipments-web/browser /usr/share/nginx/html
EXPOSE 80