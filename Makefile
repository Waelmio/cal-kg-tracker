dev:
	make -j2 dev-backend dev-frontend

dev-backend:
	cd backend/WeightTracker.Api && dotnet run

dev-frontend:
	cd frontend/weight-tracker-ui && pnpm run dev

test:
	make -j2 test-backend test-frontend
	make test-integration

test-backend:
	cd backend && dotnet test WeightTracker.UTest/WeightTracker.Tests.csproj

test-integration:
	cd backend && dotnet test WeightTracker.ITest/WeightTracker.ITest.csproj --verbosity normal

test-frontend:
	cd frontend/weight-tracker-ui && pnpm test
