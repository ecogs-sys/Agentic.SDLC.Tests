# syntax=docker/dockerfile:1

# ── Stage 1: build ────────────────────────────────────────────────────────────
FROM node:20-alpine AS builder
WORKDIR /app

ARG VITE_API_BASE_URL
ENV VITE_API_BASE_URL=${VITE_API_BASE_URL}

# Install dependencies first for better layer caching.
COPY package.json package-lock.json ./
RUN npm ci

# Copy the rest of the source and build.
COPY . .
RUN npm run build

# ── Stage 2: runtime ──────────────────────────────────────────────────────────
FROM nginx:alpine AS runtime

# Copy built assets from the builder stage.
COPY --from=builder /app/dist /usr/share/nginx/html

# Copy custom nginx config with SPA fallback.
COPY frontend/nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
