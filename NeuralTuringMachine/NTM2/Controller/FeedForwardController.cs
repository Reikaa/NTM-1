﻿using System;
using NTM2.Memory;

namespace NTM2.Controller
{
    //TODO refactor extract layers - input, hidden and output
    class FeedForwardController : IController
    {
        #region Fields and variables

        private readonly int _controllerSize;
        private readonly int _inputSize;
        private readonly int _headCount;
        private readonly int _memoryUnitSizeM;
        private readonly UnitFactory _unitFactory;

        //Controller hidden layer threshold weights
        private readonly Unit[] _hiddenLayerThresholds;

        //Weights from input to controller
        private readonly Unit[][] _inputToHiddenLayerWeights;

        //Weights from read data to controller
        private readonly Unit[][][] _readDataToHiddenLayerWeights;

        public int HiddenLayerSize
        {
            get { return _controllerSize; }
        }

        #endregion

        #region Ctor

        public FeedForwardController(int controllerSize, int inputSize, int headCount, int memoryUnitSizeM, UnitFactory unitFactory)
        {
            _controllerSize = controllerSize;
            _inputSize = inputSize;
            _headCount = headCount;
            _memoryUnitSizeM = memoryUnitSizeM;
            _unitFactory = unitFactory;

            _readDataToHiddenLayerWeights = _unitFactory.GetTensor3(controllerSize, headCount, memoryUnitSizeM);
            _inputToHiddenLayerWeights = _unitFactory.GetTensor2(controllerSize, inputSize);
            _hiddenLayerThresholds = _unitFactory.GetVector(controllerSize);
        }

        #endregion

        #region Forward propagation

        //TODO refactor - do not use tempsum - but beware of rounding issues

        public double ForwardPropagation(double tempSum, int neuronIndex, double[] input, ReadData[] readData)
        {
            double sum = tempSum;
            sum = GetReadDataContributionToHiddenLayer(neuronIndex, readData, sum);
            sum = GetInputContributionToHiddenLayer(neuronIndex, input, sum);
            sum = GetThresholdContributionToHiddenLayer(neuronIndex, sum);
            return sum;
        }

        private double GetReadDataContributionToHiddenLayer(int neuronIndex, ReadData[] readData, double tempSum)
        {
            Unit[][] readWeightsForEachHead = _readDataToHiddenLayerWeights[neuronIndex];
            for (int headIndex = 0; headIndex < _headCount; headIndex++)
            {
                Unit[] headWeights = readWeightsForEachHead[headIndex];
                ReadData read = readData[headIndex];

                for (int memoryCellIndex = 0; memoryCellIndex < _memoryUnitSizeM; memoryCellIndex++)
                {
                    tempSum += headWeights[memoryCellIndex].Value * read.Data[memoryCellIndex].Value;
                }
            }
            return tempSum;
        }

        private double GetInputContributionToHiddenLayer(int neuronIndex, double[] input, double tempSum)
        {
            Unit[] inputWeights = _inputToHiddenLayerWeights[neuronIndex];
            for (int j = 0; j < inputWeights.Length; j++)
            {
                tempSum += inputWeights[j].Value * input[j];
            }
            return tempSum;
        }

        private double GetThresholdContributionToHiddenLayer(int neuronIndex, double tempSum)
        {
            tempSum += _hiddenLayerThresholds[neuronIndex].Value;
            return tempSum;
        }

        #endregion

        #region Update weights

        public void UpdateWeights(Action<Unit> updateAction)
        {
            Action<Unit[]> vectorUpdateAction = Unit.GetVectorUpdateAction(updateAction);
            Action<Unit[][]> tensor2UpdateAction = Unit.GetTensor2UpdateAction(updateAction);
            Action<Unit[][][]> tensor3UpdateAction = Unit.GetTensor3UpdateAction(updateAction);

            tensor3UpdateAction(_readDataToHiddenLayerWeights);
            tensor2UpdateAction(_inputToHiddenLayerWeights);
            vectorUpdateAction(_hiddenLayerThresholds);
        }

        #endregion

        #region BackwardErrorPropagation

        public void BackwardErrorPropagation(double[] hiddenLayerGradients, double[] input, ReadData[] reads)
        {
            UpdateReadDataGradient(hiddenLayerGradients, reads);

            UpdateInputToHiddenWeightsGradients(hiddenLayerGradients, input);

            UpdateHiddenLayerThresholdsGradients(hiddenLayerGradients);
        }

        private void UpdateReadDataGradient(double[] hiddenLayerGradients, ReadData[] reads)
        {
            for (int neuronIndex = 0; neuronIndex < _controllerSize; neuronIndex++)
            {
                Unit[][] neuronToReadDataWeights = _readDataToHiddenLayerWeights[neuronIndex];
                double hiddenLayerGradient = hiddenLayerGradients[neuronIndex];

                for (int headIndex = 0; headIndex < _headCount; headIndex++)
                {
                    ReadData readData = reads[headIndex];
                    Unit[] neuronToHeadReadDataWeights = neuronToReadDataWeights[headIndex];
                    for (int memoryCellIndex = 0; memoryCellIndex < _memoryUnitSizeM; memoryCellIndex++)
                    {
                        readData.Data[memoryCellIndex].Gradient += hiddenLayerGradient * neuronToHeadReadDataWeights[memoryCellIndex].Value;

                        neuronToHeadReadDataWeights[memoryCellIndex].Gradient += hiddenLayerGradient * readData.Data[memoryCellIndex].Value;
                    }
                }
            }
        }

        private void UpdateInputToHiddenWeightsGradients(double[] hiddenLayerGradients, double[] input)
        {
            for (int neuronIndex = 0; neuronIndex < _controllerSize; neuronIndex++)
            {
                double hiddenGradient = hiddenLayerGradients[neuronIndex];
                Unit[] inputToHiddenNeuronWeights = _inputToHiddenLayerWeights[neuronIndex];

                UpdateInputGradient(hiddenGradient, inputToHiddenNeuronWeights, input);
            }
        }

        private void UpdateInputGradient(double hiddenLayerGradient, Unit[] inputToHiddenNeuronWeights, double[] input)
        {
            for (int inputIndex = 0; inputIndex < _inputSize; inputIndex++)
            {
                inputToHiddenNeuronWeights[inputIndex].Gradient += hiddenLayerGradient * input[inputIndex];
            }
        }

        private void UpdateHiddenLayerThresholdsGradients(double[] hiddenLayerGradients)
        {
            for (int neuronIndex = 0; neuronIndex < _controllerSize; neuronIndex++)
            {
                _hiddenLayerThresholds[neuronIndex].Gradient += hiddenLayerGradients[neuronIndex];
            }
        }

        #endregion
    }
}