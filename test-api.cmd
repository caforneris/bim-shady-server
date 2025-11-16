@echo off
REM BimShady API Test Script
REM Usage: test-api.cmd [command]
REM Commands: health, ping, project, categories, import, import-file, walls-rect

set BASE_URL=http://localhost:8080/api

if "%1"=="" goto menu
if "%1"=="health" goto health
if "%1"=="ping" goto ping
if "%1"=="project" goto project
if "%1"=="categories" goto categories
if "%1"=="import" goto import_file
if "%1"=="import-file" goto import_file
if "%1"=="walls-rect" goto walls_rect
if "%1"=="walls-single" goto walls_single
if "%1"=="schedule" goto schedule
if "%1"=="sheet" goto sheet
if "%1"=="export" goto export
if "%1"=="full-pipeline" goto full_pipeline
if "%1"=="import-plan" goto import_plan
if "%1"=="import-sketch" goto import_sketch
goto menu

:menu
echo.
echo BimShady API Test Commands:
echo   test-api health        - Server health check
echo   test-api ping          - Ping Revit thread
echo   test-api project       - Get project info
echo   test-api categories    - List all categories
echo   test-api import-file   - Import from output.json (simple)
echo   test-api import-plan   - Import comprehensive plan with doors/fixtures
echo   test-api import-sketch - Import sketch (walls, doors, rooms from pixels)
echo   test-api walls-rect    - Create rectangle (4 walls)
echo   test-api walls-single  - Create single test wall
echo   test-api schedule      - Create room schedule
echo   test-api sheet         - Create sheet with views
echo   test-api export        - Export sheet to PDF/DWG
echo   test-api full-pipeline - Run complete sketch-to-BIM pipeline
echo.
goto end

:health
echo Testing: GET /api/health
curl -s %BASE_URL%/health
echo.
goto end

:ping
echo Testing: GET /api/ping
curl -s %BASE_URL%/ping
echo.
goto end

:project
echo Testing: GET /api/project
curl -s %BASE_URL%/project
echo.
goto end

:categories
echo Testing: GET /api/categories
curl -s %BASE_URL%/categories
echo.
goto end

:import_file
echo Testing: POST /api/import (from output.json)
curl -s -X POST %BASE_URL%/import -H "Content-Type: application/json" -d @C:\Users\craig.forneris\Downloads\output.json
echo.
goto end

:walls_rect
echo Testing: POST /api/walls (rectangle)
curl -s -X POST %BASE_URL%/walls -H "Content-Type: application/json" -d "{\"walls\":[{\"startX\":0,\"startY\":0,\"endX\":20,\"endY\":0,\"height\":10},{\"startX\":20,\"startY\":0,\"endX\":20,\"endY\":15,\"height\":10},{\"startX\":20,\"startY\":15,\"endX\":0,\"endY\":15,\"height\":10},{\"startX\":0,\"startY\":15,\"endX\":0,\"endY\":0,\"height\":10}]}"
echo.
goto end

:walls_single
echo Testing: POST /api/walls (single wall)
curl -s -X POST %BASE_URL%/walls -H "Content-Type: application/json" -d "{\"walls\":[{\"startX\":0,\"startY\":0,\"endX\":10,\"endY\":0,\"height\":10}]}"
echo.
goto end

:schedule
echo Testing: POST /api/schedule (create room schedule)
curl -s -X POST %BASE_URL%/schedule
echo.
goto end

:sheet
echo Testing: POST /api/sheet (create documentation sheet)
curl -s -X POST %BASE_URL%/sheet
echo.
goto end

:export
echo Testing: POST /api/export (export sheet to PDF/DWG)
curl -s -X POST %BASE_URL%/export
echo.
goto end

:full_pipeline
echo ========================================
echo Running Full Sketch-to-BIM Pipeline
echo ========================================
echo.
echo Step 1: Import sketch (walls, doors, rooms with centered tags)...
curl -s -X POST %BASE_URL%/import-sketch -H "Content-Type: application/json" -d @C:\Users\craig.forneris\Downloads\output.json
echo.
echo.
echo Step 2: Create room schedule...
curl -s -X POST %BASE_URL%/schedule
echo.
echo.
echo Step 3: Create documentation sheet (with floor plan, 3D view, and schedule)...
curl -s -X POST %BASE_URL%/sheet
echo.
echo.
echo Step 4: Export sheet to PDF/DWG...
curl -s -X POST %BASE_URL%/export
echo.
echo.
echo ========================================
echo Pipeline Complete!
echo ========================================
goto end

:import_plan
echo Testing: POST /api/import-plan (comprehensive floor plan)
REM curl -s -X POST %BASE_URL%/import-plan -H "Content-Type: application/json" -d @C:\Users\craig.forneris\Downloads\comprehensive_plan.json
curl -s -X POST %BASE_URL%/import-plan -H "Content-Type: application/json" -d @C:\Users\craig.forneris\Downloads\floorplan_converted.json
echo.
goto end

:import_sketch
echo Testing: POST /api/import-sketch (sketch from drawing app)
curl -s -X POST %BASE_URL%/import-sketch -H "Content-Type: application/json" -d @C:\Users\craig.forneris\Downloads\output.json
echo.
goto end

:end
