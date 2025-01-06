# Newton-Raphson square root approximation

import numpy as np
import matplotlib.pyplot as plt
from matplotlib.widgets import Slider

# Define the function for the Newton-Raphson square root approximation
def sqrt_newton_raphson(S, initial_guess, num_iterations):
    x = initial_guess
    for _ in range(num_iterations):
        x = 0.5 * (x + S / x)
    return x

# Set up the figure and the plot
fig, ax = plt.subplots()
plt.subplots_adjust(left=0.1, bottom=0.25)

# Initial parameters
S = 25  # Example value for S (e.g., radius - dst)
initial_guess = S / 2.0
num_iterations = 1  # Start with 1 iteration

# Initial plot
x_vals = np.arange(1, 11, 1)  # Iteration counts from 1 to 10
y_vals = [sqrt_newton_raphson(S, initial_guess, i) for i in x_vals]
line, = plt.plot(x_vals, y_vals, lw=2)
ax.set_xlabel('Iteration Count')
ax.set_ylabel('Approximated Square Root Value')
ax.set_title(r'Newton-Raphson Square Root Approximation')

# Add sliders for dynamic adjustment
axcolor = 'lightgoldenrodyellow'
axiter = plt.axes([0.1, 0.1, 0.65, 0.03], facecolor=axcolor)

siter = Slider(axiter, 'Iterations', 1, 10, valinit=1, valstep=1)

# Update function for the slider
def update(val):
    num_iter = int(siter.val)
    y_vals = [sqrt_newton_raphson(S, initial_guess, i) for i in x_vals]
    line.set_ydata(y_vals)
    fig.canvas.draw_idle()

siter.on_changed(update)

plt.show()
