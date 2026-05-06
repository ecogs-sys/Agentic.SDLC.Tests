# Stage 1: Build the React SPA
FROM node:20-alpine AS build

WORKDIR /app

# Accept VITE_API_BASE_URL as a build argument (default /api)
ARG VITE_API_BASE_URL=/api
ENV VITE_API_BASE_URL=${VITE_API_BASE_URL}

# Copy package files and install dependencies
COPY package.json package-lock.json ./
RUN npm ci

# Copy source and build the bundle
COPY . .
RUN npm run build

# Stage 2: Serve the built SPA with Nginx
FROM nginx:alpine AS runtime

# Remove default nginx config
RUN rm /etc/nginx/conf.d/default.conf

# Copy custom nginx config
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copy built assets from build stage into nginx html root
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
