dev:
	make -j2 dev-backend dev-frontend

dev-backend:
	cd backend/WeightTracker.Api && dotnet run

dev-frontend:
	cd frontend/weight-tracker-ui && pnpm run dev

test:
	make -j2 test-backend test-frontend

test-backend:
	cd backend && dotnet test

test-frontend:
	cd frontend/weight-tracker-ui && pnpm test
