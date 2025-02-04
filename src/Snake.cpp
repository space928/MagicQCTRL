#include "Config.h"
#include "debug.h"
#include "Globals.h"
#include "Display.h"

enum MoveDir : uint8_t
{
    Up = 0,
    Down = 1,
    Left = 2,
    Right = 3
};

enum CellType : uint8_t
{
    Empty = 0,
    Snake = 1,
    SnakeHead = 2,
    SnakeTail = 3,
    Food = 4
};

struct Vec2
{
    int16_t x;
    int16_t y;
};

// ===================================
// LOCALS
#define SNAKE_CELL_SIZE 4
#define SNAKE_CELL_COUNT (DISPLAY_WIDTH / SNAKE_CELL_SIZE * DISPLAY_HEIGHT / SNAKE_CELL_SIZE)
#define SNAKE_GRID_WIDTH (DISPLAY_WIDTH / SNAKE_CELL_SIZE)
#define SNAKE_GRID_HEIGHT (DISPLAY_HEIGHT / SNAKE_CELL_SIZE)

int16_t snake_x = DISPLAY_WIDTH / 2;
int16_t snake_y = DISPLAY_HEIGHT / 2;
int16_t move_rate = 100;
int snake_head = 0;
int snake_tail = 0;
int score = 0;
Vec2 food_pos = {0,0};
Vec2 snake_cells[SNAKE_CELL_COUNT];
CellType snake_grid[SNAKE_CELL_COUNT];
ulong last_move_time = 0;
MoveDir move_dir = MoveDir::Up;

// ===================================
// FUNCTIONS

void createFood();
void drawFood();
uint32_t fnv_hash(int32_t x);
void deathScreen();

void initSnake()
{
    auto_clear_display = false;
    display.clearDisplay();
    pixels.clear();

    pixels.setPixelColor(2, 200, 0, 10); // Close

    pixels.setPixelColor(7, 200, 255, 10);  // Up
    pixels.setPixelColor(9, 200, 255, 10);  // Left
    pixels.setPixelColor(10, 200, 255, 10); // Down
    pixels.setPixelColor(11, 200, 255, 10); // Right

    snake_head = 2;
    snake_tail = 0;
    score = 0;
    int16_t centre_x = DISPLAY_WIDTH / SNAKE_CELL_SIZE / 2;
    int16_t centre_y = DISPLAY_HEIGHT / SNAKE_CELL_SIZE / 2;
    snake_cells[0] = {centre_x, (int16_t)(centre_y + 2)};
    snake_cells[1] = {centre_x, (int16_t)(centre_y + 1)};
    snake_cells[2] = {centre_x, centre_y};

    for (int i = 0; i < SNAKE_CELL_COUNT; i++)
        snake_grid[i] = CellType::Empty;

    createFood();
}

void drawSnake()
{
    ulong t = millis();
    if (t - last_move_time >= move_rate)
    {
        last_move_time = t;

        display.setCursor(0, DISPLAY_HEIGHT - 8);
        display.print("SCORE: ");
        display.print(score);

        // Compute new head pos
        Vec2 last_head = snake_cells[snake_head];
        Vec2 head;
        Vec2 draw_offset; // These are needed to offset the rectangle drawing so that the segments of the snake connect correctly
        Vec2 draw_size;
        switch (move_dir)
        {
        case MoveDir::Up:
            head = {last_head.x, (int16_t)(last_head.y - 1)};
            if (head.y < 0)
                head.y = SNAKE_GRID_HEIGHT - 1;
            draw_offset = {0, 0};
            draw_size = {SNAKE_CELL_SIZE - 1, SNAKE_CELL_SIZE};
            break;
        case MoveDir::Down:
            head = {last_head.x, (int16_t)(last_head.y + 1)};
            if (head.y >= SNAKE_GRID_HEIGHT)
                head.y = 0;
            draw_offset = {0, -1};
            draw_size = {SNAKE_CELL_SIZE - 1, SNAKE_CELL_SIZE};
            break;
        case MoveDir::Left:
            head = {(int16_t)(last_head.x - 1), last_head.y};
            if (head.x < 0)
                head.x = SNAKE_GRID_WIDTH - 1;
            draw_offset = {0, 0};
            draw_size = {SNAKE_CELL_SIZE, SNAKE_CELL_SIZE - 1};
            break;
        case MoveDir::Right:
            head = {(int16_t)(last_head.x + 1), last_head.y};
            if (head.x >= SNAKE_GRID_WIDTH)
                head.x = 0;
            draw_offset = {-1, 0};
            draw_size = {SNAKE_CELL_SIZE, SNAKE_CELL_SIZE - 1};
            break;
        }

        // Check for collisions
        switch (snake_grid[head.x + head.y * SNAKE_GRID_WIDTH])
        {
            case CellType::Empty:
                break;
            case CellType::Snake:
                deathScreen();
                return;
            case CellType::Food:
                snake_tail--;
                score++;
                createFood();
                break;
        }

        // Advance head and tail
        // Clear the end of the tail
        Vec2 tail = snake_cells[snake_tail];
        display.fillRect(tail.x * SNAKE_CELL_SIZE, tail.y * SNAKE_CELL_SIZE, SNAKE_CELL_SIZE, SNAKE_CELL_SIZE, 0);
        snake_grid[tail.x + tail.y * SNAKE_GRID_WIDTH] = CellType::Empty;
        snake_tail++;
        // snake_grid[tail.x+tail.y*SNAKE_GRID_WIDTH] = CellType::SnakeTail;

        // Advance head
        snake_head++;
        if (snake_head >= SNAKE_CELL_COUNT)
            snake_head = 0;
        snake_cells[snake_head] = head;
        snake_grid[head.x + head.y * SNAKE_GRID_WIDTH] = CellType::Snake;
        display.fillRect(head.x * SNAKE_CELL_SIZE + draw_offset.x, head.y * SNAKE_CELL_SIZE + draw_offset.y, draw_size.x, draw_size.y, 1);

        // Wrap circular buffer
        if (snake_tail >= SNAKE_CELL_COUNT)
            snake_tail = 0;

        drawFood();

        display.display();

        wakeDisplay();
    }

    if (switch_states[2])
    {
        auto_clear_display = true;
        display_page = N_PAGES;
    }

    if (switch_states[7])
        move_dir = MoveDir::Up;
    else if (switch_states[9])
        move_dir = MoveDir::Left;
    else if (switch_states[10])
        move_dir = MoveDir::Down;
    else if (switch_states[11])
        move_dir = MoveDir::Right;

    pixels.clear();

    pixels.setPixelColor(2, 200, 0, 5); // Close

    pixels.setPixelColor(7, 100, 255, 5);  // Up
    pixels.setPixelColor(9, 100, 255, 5);  // Left
    pixels.setPixelColor(10, 100, 255, 5); // Down
    pixels.setPixelColor(11, 100, 255, 5); // Right

    delay(2);
}

void deathScreen()
{
    display.clearDisplay();
    for (int i = 0; i < 5; i++) {
        display.clearDisplay();
        display.setTextColor(1, 0);
        display.setCursor(DISPLAY_WIDTH / 2 - 9 * 3, DISPLAY_HEIGHT / 2);
        display.print("GAME OVER");
        display.display();
        delay(60);

        display.fillScreen(1);
        display.setTextColor(0, 0);
        display.setCursor(DISPLAY_WIDTH / 2 - 9 * 3, DISPLAY_HEIGHT / 2);
        display.print("GAME OVER");
        display.display();
        delay(60);
    }
    display.setTextColor(1, 0);
    delay(60);
    
    initSnake();
}

uint32_t fnv_hash(int32_t x)
{
    const uint32_t prime = 16777619;
    uint32_t hash = 2166136261;
    
    hash ^= (x >> 0) & 0xff;
    hash *= prime;

    hash ^= (x >> 4) & 0xff;
    hash *= prime;

    hash ^= (x >> 8) & 0xff;
    hash *= prime;

    hash ^= (x >> 12) & 0xff;
    hash *= prime;

    return hash;
}

void createFood()
{
    for (int16_t i = 0; i < SNAKE_CELL_COUNT; i++)
    {
        uint32_t rnd = fnv_hash(i + score << 4);
        Vec2 pos = {
            (int16_t)((rnd & 0xffffu) % SNAKE_GRID_WIDTH),
            (int16_t)(((rnd>>16) & 0xffffu) % SNAKE_GRID_HEIGHT),
        };

        if (snake_grid[pos.x + pos.y * SNAKE_GRID_WIDTH] == CellType::Empty)
        {
            food_pos = pos;
            snake_grid[pos.x + pos.y * SNAKE_GRID_WIDTH] = CellType::Food;

            return;
        }
    }
    // Fallback if no random numbers work
    for (int16_t i = 0; i < SNAKE_CELL_COUNT; i++)
    {
        if (snake_grid[i] == CellType::Empty)
        {
            Vec2 pos = {
                (int16_t)(i % SNAKE_GRID_WIDTH),
                (int16_t)(i / SNAKE_GRID_WIDTH),
            };
            food_pos = pos;
            snake_grid[pos.x + pos.y * SNAKE_GRID_WIDTH] = CellType::Food;

            return;
        }
    }
}

void drawFood()
{
    display.drawFastHLine(food_pos.x * SNAKE_CELL_SIZE + 1, food_pos.y * SNAKE_CELL_SIZE, SNAKE_CELL_SIZE - 2, 1);
    display.drawFastHLine(food_pos.x * SNAKE_CELL_SIZE + 1, food_pos.y * SNAKE_CELL_SIZE + SNAKE_CELL_SIZE - 1, SNAKE_CELL_SIZE - 2, 1);
    display.drawFastVLine(food_pos.x * SNAKE_CELL_SIZE, food_pos.y * SNAKE_CELL_SIZE + 1, SNAKE_CELL_SIZE - 2, 1);
    display.drawFastVLine(food_pos.x * SNAKE_CELL_SIZE + SNAKE_CELL_SIZE - 1, food_pos.y * SNAKE_CELL_SIZE + 1, SNAKE_CELL_SIZE - 2, 1);
}
