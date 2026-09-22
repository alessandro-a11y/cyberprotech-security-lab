# Execução pontual do toolkit (perfil separado, fora do `up` padrão).
# Uso: docker compose --profile toolkit run --rm toolkit --target http://backend:8080
FROM python:3.12-slim
WORKDIR /app
COPY toolkit/requirements.txt ./
RUN pip install --no-cache-dir -r requirements.txt
COPY toolkit/ ./toolkit/
ENV PYTHONPATH=/app/toolkit/src
ENTRYPOINT ["python", "-m", "toolkit.cli"]
