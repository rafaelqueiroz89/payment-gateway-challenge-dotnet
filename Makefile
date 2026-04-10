.PHONY: all build test run docker-up docker-down

all: build test

build:
	dotnet build PaymentGateway.sln

test:
	dotnet test PaymentGateway.sln

run:
	dotnet run --project src/PaymentGateway.Api

docker-up:
	docker-compose up -d

docker-down:
	docker-compose down
