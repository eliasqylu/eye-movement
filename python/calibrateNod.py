#!/usr/bin/env python
# coding: utf-8

# In[2]:


import numpy as np
import pandas as pd
from collections import deque
from sklearn.model_selection import GroupKFold
from sklearn.metrics import accuracy_score, balanced_accuracy_score, f1_score, confusion_matrix
from bayes_opt import BayesianOptimization
import matplotlib.pyplot as plt

import os
os.environ['CUDA_LAUNCH_BLOCKING'] = '1'

import pickle
import glob
import pandas as pd

import itertools

from scipy.spatial.transform import Rotation as R
from collections import deque
from scipy.linalg import logm

def getEventPeriod(eventArray):
    '''
    find the first and last sample index for each events labelled in a binary(0,1) eventArray
    Input:
        evetnArray: binary array containing the labels of each sample, for example, [0,0,1,1,0, 1,1], ie. none, none, fixation, fixation, none, fixaiton, fixation
    Output:
        eventPeriod: Array of the first and last sample indices of each indivisual event. array of shape [n, 2],  there are n individual events.
                      for exmple, [[2,3], [5,6]], is the eventPeriod corresponding to eventArray=[0,0,1,1,0, 1,1]
    '''
    # each element in eventStartInd is the index of the first sample in an individual event, where eventArray == 1
    eventStartInd = np.where(np.diff(eventArray)==1)[0] + 1
    if len(eventStartInd)==0 and eventArray[0]==1:
        eventStartInd = [0]
    # each element in eventEndInd is the index of the last sample in an individual event, where eventArray == 1
    eventEndInd = np.where(np.diff(eventArray)==-1)[0]
    
    if eventArray.any() == False:
        return np.array([])
    
    if len(eventStartInd) > 0 and len(eventEndInd)==0:
        eventEndInd = np.array([len(eventArray)-1])
    

    if eventEndInd[0] < eventStartInd[0]:
        eventStartInd = np.insert(eventStartInd, 0, 0)
    if eventEndInd[-1] < eventStartInd[-1]:
        eventEndInd = np.insert(eventEndInd, len(eventEndInd), len(eventArray)-1)

    eventPeriod = np.array([eventStartInd, eventEndInd]).T
    
    return eventPeriod

def rms(x, y):
    rms = np.sqrt(np.nanmean(np.diff(x)**2 + np.diff(y)**2))
    return rms

def vec2ficks(x, y, z):
    '''
    Per Baekgaard 2022
    '''
    r = np.sqrt(x**2 + y**2 + z**2)
    az = np.arctan2(x , z) # Note: If we had done az = 90 - np.arccos(x / r) we would return visual angles
    pol = np.arccos(y / r)

    return np.rad2deg(az), 90 - np.rad2deg(pol)

def local2World(localX, localY, localZ, local2WorldMatrix):
    '''
    Inputs:
        localX/Y/Z: array of local coordinates
        local2WorldMatrix: nX4X4 matrix, where n is number of samples, such that worldCoordinates = local2WorldMatrix * localCoordinates
    Outputs:
        vecWorld[:,0], vecWorld[:,1], vecWorld[:,2]: array of world coordintates
    '''
    #### Local to World
    vecLocal = np.array([localX, localY, localZ, np.zeros(len(localX),)]).T

    #transform local to world coord
    vecWorld = []
    for i in range(0, len(local2WorldMatrix)):
        vecWorld.append(np.dot(local2WorldMatrix[i], vecLocal[i].T))
    vecWorld = np.array(vecWorld)
    
    return vecWorld[:,0], vecWorld[:,1], vecWorld[:,2]


def getNearestFiniteInd (X, zeroInd):
    '''
    Get the closest index about zeroInd, where X is finite.
    Input:
        X: array, e.g. eyeInWorldAz
        zeroInd: the index where head is centered, i.e. frameOfCentering
    Output:
        the closest index about zeroInd, where X is finite. If difference to zeroInd is greater than 10, returns NaN.
    '''
    count_left = 0
    while True:
        if np.isfinite(X[zeroInd-count_left]):
            break
        else:
            count_left += 1

    count_right = 0
    while True:
        if np.isfinite(X[zeroInd+count_right]):
            break
        else:
            count_right += 1

    if count_left < count_right:
        count = -count_left
    else:
        count = count_right
    
    if count > 10:
        count = np.nan
        
    return zeroInd+count


def idt_fixation_detection(x, y, timestamps, mindur=0.08, dispersion_thresholds=1.0):
    """
    Implements the I-DT fixation detection algorithm.

    Args:
        x: Array/list of x-coordinates.
        y: Array/list of y-coordinates.
        timestamps: Array/list of timestamps (in seconds).
        mindur: Minimum fixation duration (in seconds).
        dispersion_threshold: Dispersion threshold (in degree, or same unit as x and y).

    Returns:
        A list of lists, where each sublist contains the start and end indices of a fixation.
        Returns an empty list if no fixations are detected.
    """

    if len(x) != len(y) or len(x) != len(timestamps):
        raise ValueError("x, y, and timestamps must have the same length.")
        
    fixations = []
    current_fixation_start = None

    i = 0
    while i < len(x):
        if current_fixation_start is None:
            current_fixation_start = i

        # Calculate dispersion within the current window
        window_x = x[current_fixation_start:i+1]
        window_y = y[current_fixation_start:i+1]
        dispersion = (np.max(window_x) - np.min(window_x)) + (np.max(window_y) - np.min(window_y))
        
        if isinstance(dispersion_thresholds, (list, tuple, np.ndarray)):
            dispersion_threshold = dispersion_thresholds[i]
        else:
            dispersion_threshold = dispersion_thresholds
            
        if dispersion > dispersion_threshold:
            # Check if the previous window was a valid fixation
            if current_fixation_start is not None and timestamps[i] - timestamps[current_fixation_start] >= mindur:
                fixations.append([current_fixation_start, i - 1])  # End is exclusive
            current_fixation_start = None  # Reset for the next fixation
            i -= 1 #Re-evaluate current point as potential start of next fixation

        i += 1

    # Check for a final fixation if the loop ends
    if current_fixation_start is not None and timestamps[i-1] - timestamps[current_fixation_start] >= mindur:
        fixations.append([current_fixation_start, i - 1])

    return fixations

def ivt_dectection(gazeVel, velThreshold = 30):
    isSaccade = np.zeros(len(gazeVel))
    isSaccade[np.where(gazeVel > velThreshold)] = 1
    return getEventPeriod(isSaccade)



def centralDiff_velocity(x, dt):
    """
    Input: 
        x: rray like, 5 points
        dt: float, time step size
    Output:
        first derivative of the xs at center position
    """
    if len(x) != 5:
        raise ValueError("Input array must have 5 points.")

    return (-x[4] + 8*x[3] - 8*x[1] + x[0]) / (12 * dt)


def centralDiff_acceleration(x, dt):
    """
    Input:
        x: array like, 5 points
        dt: float, time step size
    Output:
        second derivative of the xs at center position
    """
    if len(x) != 5:
        raise ValueError("Input array must have 5 points.")

    return (-x[4] + 16*x[3] - 30*x[2] + 16*x[1] - x[0]) / (12 * dt**2)

def centralDiff_jolt(x, dt):
    """
    Input:
        x: array like, 5 points
        dt: float, time step size
    Output:
        third derivative of the xs at center position
    """
    if len(x) != 5:
        raise ValueError("Input array must have 5 points.")

    return (x[4] - 2*x[3] + 2*x[1] - x[0]) / (2 * dt**3)

def sliding_window_derivative(xs, ts, n):
    """
    Calculates the nth derivative using a sliding window view.

    Args:
        xs: A numpy array of position data (n > 5).
        ts: A numpy array of timestamps
        n: derivative order to calculate

    Returns:
        A numpy array of first derivative values. The first and last two
        elements will be np.nan.
    """

    if len(xs) <= 5:
        raise ValueError("Input array must have more than 5 elements.")

    derivatives = np.full(len(xs), np.nan)
    windows = np.lib.stride_tricks.sliding_window_view(xs, 5)
    ts_windows = np.lib.stride_tricks.sliding_window_view(ts, 5)
    dts = (ts_windows[:, 3] - ts_windows[:, 1]) / 2.0
    
    if n == 1:
        derivatives[2:-2] = np.array([centralDiff_velocity(window, dt) for window, dt in zip(windows, dts)])
    elif n == 2:
        derivatives[2:-2] = np.array([centralDiff_acceleration(window, dt) for window, dt in zip(windows, dts)])  
    elif n == 3:
        derivatives[2:-2] = np.array([centralDiff_jolt(window, dt) for window, dt in zip(windows, dts)]) 
    return derivatives

def sliding_window_rms(x, y, window_size=900):
    """
    Calculates RMS for sliding windows of x and y.

    Args:
        x: numpy array of x values.
        y: numpy array of y values.
        window_size: size of the sliding window (default 900).

    Returns:
        numpy array of RMS values for each window. First window_size elements are np.nan.
    """
    if len(x) <= window_size:
        raise ValueError("Input arrays must have more than window_size elements.")

    rms_values = np.full(len(x), np.nan)
    x_windows = np.lib.stride_tricks.sliding_window_view(x, window_size)
    y_windows = np.lib.stride_tricks.sliding_window_view(y, window_size)

    for i, (x_window, y_window) in enumerate(zip(x_windows, y_windows)):
        rms_values[window_size + i - 1] = rms(x_window, y_window)
    
    rms_values[0:window_size] = rms_values[window_size]

    return rms_values


def find_matching_period_index(periodsA, periodsB, mInd):
    """
    Finds the row index in isHeadFixation that satisfies the given conditions.

    Args:
        candidateMinHeadPosInCanConfirmPeriod_periods: A list of lists representing periods.
        isHeadFixation: A list of lists representing head fixation intervals.
        mInd: The row index in candidateMinHeadPosInCanConfirmPeriod_periods.

    Returns:
        The row index in isHeadFixation, or None if no match is found.
    """

    target_start = periodsA[mInd][0]
    if len(periodsB) ==0:
        return 0

    for xInd in range(len(periodsB) - 1):
        if target_start <= periodsB[xInd + 1][0] and target_start >= periodsB[xInd][1]:
            return xInd
    if target_start >= periodsB[len(periodsB)-1][1]:
        return len(periodsB) - 1
    return None


def collectArray(df, fname):
    _array = np.ones((len(np.unique(df['trialIndex'].values)), 45)) * np.nan
    count = 0
    for trInd in np.unique(df['trialIndex'].values):
        df_trial = df.loc[df['trialIndex']==trInd]
        filler = df_trial[fname].values[:45]
        _array[count][:len(filler)] = filler
        count+=1
    return _array

def calculate_distance_movement(df):
    """
    Calculates the 'distanceMovement' based on 'isHeadFixation' transitions.

    Args:
        df (pd.DataFrame): The input DataFrame with 'labels', 'headPol', and 'isHeadFixation' columns.

    Returns:
        pd.DataFrame: The DataFrame with an added 'distanceMovement' column.
    """

    df['distanceMovement'] = np.nan  # Initialize the column with NaN
    startY = np.nan  # Initialize startY

    # Identify when isHeadFixation changes from True to False
    # Shift 'isHeadFixation' to get the previous value. The first row's previous will be NaN.
    prev_is_head_fixation = df['isHeadFixation'].shift(1)

    # Iterate through the DataFrame
    for i, row in df.iterrows():
        # Check for a transition from True to False
        if prev_is_head_fixation.loc[i] == True and row['isHeadFixation'] == False:
            startY = row['headPol']
            startT = row['t']

        # If isHeadFixation is currently False and startY has been set (meaning we are in a 'false' block)
        elif row['isHeadFixation'] == False and not pd.isna(startY):
            df.loc[i, 'distanceMovement'] = (row['headPol'] - startY)

        # If isHeadFixation becomes True again, reset startY to NaN
        elif row['isHeadFixation'] == True:
            startY = np.nan

    return df


# In[4]:


motherFolder = r"D:\nodButton_code\data\\"
pid = 'p_test'



dataFolder = os.path.join(motherFolder, pid)
filename = glob.glob( os.path.join(dataFolder, '*dataLog*nod.csv'))[-1]
df = pd.read_csv(filename, decimal='.', delimiter=';')

df = df.loc[df['trialIndex']<9]
0
#reorientate eyeInWorld angles
try:
    indOfCentering = df.loc[df['frameOfCentering']==True].index[0]+1
except:
    indOfCentering = 0


t = df['unityTimestamp(s)'].values-df['unityTimestamp(s)'].values[0]

################## head #######################

headX = df['hmdWorldForward_X'].values
headY = df['hmdWorldForward_Y'].values
headZ = df['hmdWorldForward_Z'].values
headAz, headPol = vec2ficks(headX, headY, headZ)
headAz0 = headAz[getNearestFiniteInd(headAz, indOfCentering)]
headPol0 = headPol[getNearestFiniteInd(headPol, indOfCentering)]
headAz-=headAz0
headPol-=headPol0

################## eye-in-head #######################
eyeX = df['combinedEyeLocalForward_filtered_X'].values
eyeY = df['combinedEyeLocalForward_filtered_Y'].values
eyeZ = df['combinedEyeLocalForward_filtered_Z'].values
eyeAz, eyePol = vec2ficks(eyeX, eyeY, eyeZ)

################## Eye-in-World #######################
#local2worldMatrix
local2WorldMatrix = np.array([df['hmdL2W_m00'].values,df['hmdL2W_m01'].values,df['hmdL2W_m02'].values,df['hmdL2W_m03'].values,
                                df['hmdL2W_m10'].values,df['hmdL2W_m11'].values,df['hmdL2W_m12'].values,df['hmdL2W_m13'].values,
                                df['hmdL2W_m20'].values,df['hmdL2W_m21'].values,df['hmdL2W_m22'].values,df['hmdL2W_m23'].values,
                                df['hmdL2W_m30'].values,df['hmdL2W_m31'].values,df['hmdL2W_m32'].values,df['hmdL2W_m33'].values]).T
local2WorldMatrix = np.reshape(local2WorldMatrix, (-1, 4,4), 'C')
gazeX, gazeY, gazeZ  = local2World (eyeX, eyeY, eyeZ, local2WorldMatrix)
gazeAz, gazePol = vec2ficks(gazeX, gazeY, gazeZ)

gazeAz0 = gazeAz[getNearestFiniteInd(gazeAz, indOfCentering)]
gazePol0 = gazePol[getNearestFiniteInd(gazePol, indOfCentering)]
gazeAz-=gazeAz0
gazePol-=gazePol0


stimInHeadX = df['stimInHeadPosition_X'].values
stimInHeadY = df['stimInHeadPosition_Y'].values
stimInHeadZ = df['stimInHeadPosition_Z'].values
stimInHeadAz, stimInHeadPol = vec2ficks(stimInHeadX, stimInHeadY, stimInHeadZ)

cursorInHeadX = df['cursorInHeadPosition_X'].values
cursorInHeadY = df['cursorInHeadPosition_Y'].values
cursorInHeadZ = df['cursorInHeadPosition_Z'].values
cursorInHeadAz, cursorInHeadPol = vec2ficks(cursorInHeadX, cursorInHeadY, cursorInHeadZ)




# df['t'] = t
# df['headAz'] = headAz
# df['headPol'] = headPol
# df['eyeAz'] = eyeAz
# df['eyePol'] = eyePol
# df['gazeAz'] = gazeAz
# df['gazePol'] = gazePol
# df['stimInHeadAz'] = stimInHeadAz
# df['stimInHeadPol'] = stimInHeadPol
# df['cursorInHeadAz'] = cursorInHeadAz
# df['cursorInHeadPol'] = cursorInHeadPol

headVelAz = sliding_window_derivative(headAz, t, 1)
headVelPol = sliding_window_derivative(headPol, t, 1)
headVel = np.sqrt(headVelAz**2+headVelPol**2)
headAccelAz = sliding_window_derivative(headAz, t, 2)
headAccelPol = sliding_window_derivative(headPol, t, 2)
headAccel = np.sqrt(headAccelAz**2+headAccelPol**2)
headJoltAz = sliding_window_derivative(headAz, t, 3)
headJoltPol = sliding_window_derivative(headPol, t, 3)
headJolt = np.sqrt(headJoltAz**2+headJoltPol**2)
headRMS = sliding_window_rms(headAz, headPol, 180)

eyeAz, eyePol = vec2ficks(eyeX, eyeY, eyeZ)
eyeVelAz = sliding_window_derivative(eyeAz, t, 1)
eyeVelPol = sliding_window_derivative(eyePol, t, 1)
eyeVel = np.sqrt(eyeVelAz**2+eyeVelPol**2)
eyeAccelAz = sliding_window_derivative(eyeAz, t, 2)
eyeAccelPol = sliding_window_derivative(eyePol, t, 2)
eyeAccel = np.sqrt(eyeAccelAz**2+eyeAccelPol**2)
eyeRMS = sliding_window_rms(eyeAz, eyePol, 180)

gazeAz, gazePol = vec2ficks(gazeX, gazeY, gazeZ)
gazeVelAz = sliding_window_derivative(gazeAz, t, 1)
gazeVelPol = sliding_window_derivative(gazePol, t, 1)
gazeVel = np.sqrt(gazeVelAz**2+gazeVelPol**2)
gazeAccelAz = sliding_window_derivative(gazeAz, t, 2)
gazeAccelPol = sliding_window_derivative(gazePol, t, 2)
gazeAccel = np.sqrt(gazeAccelAz**2+gazeAccelPol**2)
gazeRMS = sliding_window_rms(gazeAz, gazePol, 180)



df['t'] = t
df['headAz'] = headAz
df['headPol'] = headPol
df['headVelAz'] = headVelAz
df['headVelPol'] = headVelPol
df['headVel'] = headVel
df['headAccelAz'] = headAccelAz
df['headAccelPol'] = headAccelPol
df['headAccel'] = headAccel
df['headJoltAz'] = headJoltAz
df['headJoltPol'] = headJoltPol
df['headJolt'] = headJolt

isHeadFixation = np.zeros(len(df))
fixations = idt_fixation_detection(headAz, headPol, t, 0.08, 1.0*headRMS)
for fix in fixations:
    isHeadFixation[fix[0]:fix[1]+1] = 1
df['isHeadFixation'] = isHeadFixation

isHeadSaccade = np.zeros(len(df))
isHeadSaccade[np.where(headVel > 5)] = 1
df['isHeadSaccade'] = isHeadSaccade


df['eyeAz'] = eyeAz
df['eyePol'] = eyePol
df['eyeVelAz'] = eyeVelAz
df['eyeVelPol'] = eyeVelPol
df['eyeVel'] = eyeVel
df['eyeAccelAz'] = eyeAccelAz
df['eyeAccelPol'] = eyeAccelPol
df['eyeAccel'] = eyeAccel

isEyeFixation = np.zeros(len(df))
eye_rms = rms(eyeAz, eyePol)
fixations = idt_fixation_detection(eyeAz, eyePol, t, 0.08, 1.5*eyeRMS)
for fix in fixations:
    isEyeFixation[fix[0]:fix[1]+1] = 1
df['isEyeFixation'] = isEyeFixation    

isEyeSaccade = np.zeros(len(df))
isEyeSaccade[np.where(eyeVel > 30)] = 1
df['isEyeSaccade'] = isEyeSaccade


df['gazeAz'] = gazeAz
df['gazePol'] = gazePol
df['gazeVelAz'] = gazeVelAz
df['gazeVelPol'] = gazeVelPol
df['gazeVel'] = gazeVel
df['gazeAccelAz'] = gazeAccelAz
df['gazeAccelPol'] = gazeAccelPol
df['gazeAccel'] = gazeAccel

isGazeFixation = np.zeros(len(df))
gaze_rms = rms(gazeAz, gazePol)
fixations = idt_fixation_detection(gazeAz, gazePol, t, 0.08, 1.5*gazeRMS)
for fix in fixations:
    isGazeFixation[fix[0]:fix[1]+1] = 1
df['isGazeFixation'] = isGazeFixation    

isGazeSaccade = np.zeros(len(df))
isGazeSaccade[np.where(gazeVel > 30)] = 1
df['isGazeSaccade'] = isGazeSaccade

stimAz, stimPol = vec2ficks(df['stimInHeadPosition_X'].values, 
                            df['stimInHeadPosition_Y'].values, 
                            df['stimInHeadPosition_Z'].values)
cursorAz, cursorPol = vec2ficks(df['cursorInHeadPosition_X'].values, 
                            df['cursorInHeadPosition_Y'].values, 
                            df['cursorInHeadPosition_Z'].values)

df['stimAz'] = stimAz
df['stimPol'] = stimPol
df['cursorAz'] = cursorAz
df['cursorPol'] = cursorPol


df = calculate_distance_movement(df)




if ('nod.csv' in filename):
    labels = []
    for trInd in np.unique(df['trialIndex'].values):
        dfTrial = df.loc[df['trialIndex']==trInd]
        t = dfTrial['t'].values
        headVelPol = dfTrial['headVel_Pol'].values
        label_2 = np.logical_and(dfTrial['trackPadClicked'].values==1, headVelPol<-5) 
        label_2_periods = getEventPeriod(label_2.astype(int))
        periods_long_enough = []
        for p in label_2_periods:
            if p[1]-p[0] > 5:
                periods_long_enough.append(p)
        if len(periods_long_enough)!=1:
            print(f'bad trial: {trInd}, {len(periods_long_enough)} events')
            label = [-1] * len(dfTrial)
            labels.extend(label)
            continue

        isHeadInFixation = dfTrial['isHeadFixation'].values
        for traceBackSamples in range (0,10):
            if isHeadInFixation[periods_long_enough[0][0]-traceBackSamples]:
                break

        periods_long_enough[0][0] -= min(traceBackSamples, 10)
        firstFrameClick = np.where(dfTrial['trackPadClicked'].values)[0][0]

        label = np.zeros(len(dfTrial))
        label[periods_long_enough[0][0]:periods_long_enough[0][1]+1] = 2
        magicX = int((periods_long_enough[0][1]+1-periods_long_enough[0][0])/2)
        label[periods_long_enough[0][1]+1:-magicX] = 3
        label[periods_long_enough[0][1]+1:periods_long_enough[0][1]+magicX] = 3
        label[periods_long_enough[0][1]+magicX:] = -1
        label[:firstFrameClick] = dfTrial['inputState'].values[:firstFrameClick]
        label[firstFrameClick:periods_long_enough[0][0]] += label[:firstFrameClick][-1]

        try:
            firstFrameRefine = np.where(label==1)[0][0]
            label[firstFrameRefine-0:firstFrameRefine] = 1
        except:
            label = label

        labels.extend(label)

        plt.plot(t, dfTrial['headPol'].values, 'b')
        plt.twinx()
        plt.plot(t, label, 'k', alpha = 0.1)
        plt.title(trInd)
        plt.show()
    df['labels'] = labels
else:
    print('not nod! ' +  filename)


# In[5]:


clipToNan = False
clipToZero = True

df_refine = df.loc[df['labels']==1]
df_nod = df.loc[df['labels']==2]


nodVel_array = collectArray(df_nod, 'headVelPol')
refineVel_array =  collectArray(df_refine, 'headVelPol')

if clipToNan:
    nodVel_array = np.where((nodVel_array > 0) | (nodVel_array < -150), np.nan, nodVel_array)
    refineVel_array = np.where((refineVel_array > 0) | (refineVel_array < -150), np.nan, refineVel_array)
elif clipToZero:
    nodVel_array = np.clip(nodVel_array, -150, 0)
    refineVel_array = np.clip(refineVel_array, -150, 0)


meanNod = np.nanmean(nodVel_array, axis = 0)
sdNod = 2*np.nanstd(nodVel_array, axis = 0)
meanRefine = np.nanmean(refineVel_array, axis = 0)
sdRefine = 2*np.nanstd(refineVel_array, axis = 0)

fig, axes = plt.subplots(1,2,figsize=(10,5), sharey=True)
for tr in nodVel_array:
    axes[0].plot(tr, alpha = 0.1)
    axes[0].plot(meanNod, 'b')
    axes[0].plot(meanNod+sdNod, 'c')
    axes[0].plot(meanNod-sdNod, 'c')
for tr in refineVel_array:
    axes[1].plot(tr, alpha = 0.1)
    axes[1].plot(meanRefine, 'r')
    axes[1].plot(meanRefine+sdRefine, 'pink')
    axes[1].plot(meanRefine-sdRefine, 'pink')
for ax in axes:
    ax.grid()
plt.show()




plt.plot(meanNod, 'b')
plt.plot(meanNod+sdNod, 'c')
plt.plot(meanNod-sdNod, 'c')


plt.plot(meanRefine, 'r')
plt.plot(meanRefine+sdRefine, 'pink')
plt.plot(meanRefine-sdRefine, 'pink')
plt.show()


# # Cut refine to non zeros

# In[6]:


# cutInd =20
# cutLength = 20
# refineVel_array = np.hstack([refineVel_array[:,cutInd:], np.ones((refineVel_array.shape[0], cutInd))*np.nan])
# refineVel_array = refineVel_array[:,:cutLength]
# nodVel_array = nodVel_array[:,:cutLength]




from scipy import stats
from statsmodels.stats.multitest import multipletests
from tqdm import tqdm

# --- 1. Simulate Data ---
# Let's create some sample data that matches your description.
n_trials = len(nodVel_array)
n_samples = nodVel_array.shape[1]

# Group A: Just random noise centered around 0
group_A = nodVel_array

# Group B: Same noise, but with an "effect" added between samples 20 and 35
group_B = refineVel_array

# --- 2. Visualize the Data ---
def plot_time_series_with_ci(ax, data, color, label):
    """Helper function to plot mean and 95% CI."""
    mean = np.mean(data, axis=0)
    std_err = stats.sem(data, axis=0)
    ci = std_err * stats.t.ppf((1 + 0.95) / 2., n_trials - 1)
    
    ax.plot(mean, color=color, label=label)
    ax.fill_between(range(n_samples), mean - ci, mean + ci, color=color, alpha=0.2)

fig, ax = plt.subplots(figsize=(10, 5))
plot_time_series_with_ci(ax, group_A, 'blue', 'Nod')
plot_time_series_with_ci(ax, group_B, 'red', 'Refine')
ax.set_title('Mean of Each Group with 95% Confidence Interval')
# ax.set_title("P18", fontsize=24)
ax.set_xlabel('Time Sample', fontsize=24)
ax.set_ylabel('Head Velocity (deg/s)', fontsize=24)
ax.legend(fontsize=24)
ax.grid(True, linestyle='--', alpha=0.6)
ax.tick_params(labelsize=20)
# ax.set_ylim(0,30)
plt.show()

# --- 3. Perform Point-wise T-tests ---
t_stats = np.zeros(n_samples)
p_values = np.zeros(n_samples)

for i in range(n_samples):
    # Perform independent t-test for each time point
    t_stat, p_val = stats.ttest_ind(group_A[:, i], group_B[:, i])
    t_stats[i] = t_stat
    p_values[i] = p_val

# --- 4. Correct for Multiple Comparisons ---
# Bonferroni correction
reject_bonferroni, p_values_bonf, _, _ = multipletests(p_values, alpha=0.05, method='bonferroni')
# FDR (Benjamini/Hochberg) correction
reject_fdr, p_values_fdr, _, _ = multipletests(p_values, alpha=0.05, method='fdr_bh')

# Find significant time points
significant_points_bonf = np.where(reject_bonferroni)[0]
significant_points_fdr = np.where(reject_fdr)[0]

print(f"--- Multiple Comparisons ---")
print(f"Time points found significant with Bonferroni correction: {significant_points_bonf}")
print(f"Time points found significant with FDR correction: {significant_points_fdr}")
print("\n")




# --- 5. Permutation Testing ---
print("--- Permutation Test ---")
n_permutations = 2000

# Calculate the observed maximum t-statistic from the real data
observed_max_t = np.nanmax(np.abs(t_stats))
print(f"Observed Maximum T-statistic: {observed_max_t:.4f}")

# Pool all data together for shuffling
pooled_data = np.concatenate([group_A, group_B], axis=0)
permutation_max_t_stats = np.zeros(n_permutations)

for i in tqdm(range(n_permutations), desc="Running Permutations"):
    # Shuffle the indices to randomly assign trials to new groups
    shuffled_indices = np.random.permutation(n_trials * 2)
    new_group_A = pooled_data[shuffled_indices[:n_trials], :]
    new_group_B = pooled_data[shuffled_indices[n_trials:], :]
    
    # Calculate t-stats for the shuffled data
    perm_t_stats, _ = stats.ttest_ind(new_group_A, new_group_B, axis=0)
    
    # Store the maximum absolute t-statistic from this permutation
    permutation_max_t_stats[i] = np.nanmax(np.abs(perm_t_stats))

# Calculate the p-value: the proportion of permutations where the max t-stat
# was greater than or equal to our observed max t-stat.
p_value_perm = np.sum(permutation_max_t_stats >= observed_max_t) / n_permutations
mostSigInd = np.nanargmax(np.abs(t_stats))
print(f"\nPermutation test p-value: {p_value_perm:.4f}")
if p_value_perm < 0.05:
    print("The difference between the groups is statistically significant overall.")
    print(f"The largest difference occurs around time point: {mostSigInd}")
else:
    print("The overall difference between the groups is not statistically significant.")
    
    
# (This code assumes you have already run the previous script and have
# the 'reject_fdr' boolean array from the multiple comparisons test)

def find_longest_significant_run(significance_array):
    """Finds the start and end index of the longest run of True values."""
    longest_run = 0
    longest_run_start = -1
    current_run = 0
    current_run_start = -1

    for i, is_significant in enumerate(significance_array):
        if is_significant:
            if current_run == 0:
                current_run_start = i
            current_run += 1
        else:
            if current_run > longest_run:
                longest_run = current_run
                longest_run_start = current_run_start
            current_run = 0
    
    # Final check in case the longest run is at the very end
    if current_run > longest_run:
        longest_run = current_run
        longest_run_start = current_run_start

    if longest_run_start == -1:
        return None
    
    return (longest_run_start, longest_run_start + longest_run - 1)

# Find the most significant range from the FDR results
significant_range = find_longest_significant_run(reject_fdr)

if significant_range:
    print(f"--- Most Significant Continuous Range ---")
    print(f"The most significant continuous range of samples is: {significant_range}")
    start, end = significant_range
    
    # For your classification task:
    # For a new trial, you would calculate the mean of samples from 'start' to 'end'
    # e.g., mean_value = new_trial_data[start:end+1].mean()
else:
    print("No significant continuous range was found.")
    
data = refineVel_array
mean = np.nanmean(data, axis=0)
std_err = stats.sem(data, axis=0)
ci = std_err * stats.t.ppf((1 + 0.95) / 2., n_trials - 1)
lowRefine = (mean[mostSigInd] - ci[mostSigInd])

data = nodVel_array
mean = np.nanmean(data, axis=0)
std_err = stats.sem(data, axis=0)
ci = std_err * stats.t.ppf((1 + 0.95) / 2., n_trials - 1)
highNod = (mean[mostSigInd] + ci[mostSigInd])

print("\n\n\n")
print(f"short time window in seconds: {(significant_range[1] + (np.diff(significant_range)/2))/90}")
print(f"vel threshold in short time window (deg/s): {np.max([lowRefine + 0.7*(highNod-lowRefine), highNod])}")
print(f"vel threshold for drag release nod (deg/s): {np.min([lowRefine + 0.7*(highNod-lowRefine), highNod])}")
print(f"global vel threshold deg/s: {np.nanmin(mean+ci)}")


# In[ ]:




